# Realm LFS

Realm LFS (large file storage) is an extension of the [Realm.NET SDK](http://github.com/realm/realm-dotnet) that exposes an abstraction for interacting with binary files that are transparently uploaded to a 3rd party service (e.g. S3/Azure Blob Storage) and their URL is subsequently updated in the Realm object for other clients to consume.

## Background

Using binary data (i.e. `byte[]` properties) with Realm is supported but very inefficient. The recommended approach is to upload the data to a file hosting service and store just the URL in the Realm object, but doing so requires internet connection and defeats one of the main advantages of using Realm. The purpose of this library is to abstract as much as possible the uploading part and expose an interface that is as close as possible to the Realm API of interacting with data.

## Usage

For the most part, just replace `byte[]` properties with `FileData` ones:

```csharp
public class Recipe : RealmObject
{
    public string Name { get; set; }

    public string Summary { get; set; }

    public IList<Ingredient> Ingredients { get; set; }

    // Replace this
    public byte[] Photo { get; set; }

    // with this
    public FileData Photo { get; set; }
}
```

To initialize the SDK, the minimum configuration you need to do is to configure the remote manager factory:

```csharp
FileManager.Initialize(new FileManagerOptions
{
    RemoteManagerFactory = (config) => new FunctionsFileManager(config, "MyDataFunction")
});
```

The `FileData` class can be constructed from a `Stream` - if you already have a `byte[]`, that can be used to create a `MemoryStream`.

When displaying an image from a `FileData`, the code should look something like:

```csharp
public void PopulateImage(Recipe recipe)
{
    switch (recipe.Photo.Status)
    {
        case DataStatus.Local:
            var imagePath = recipe.Photo.LocalUrl;
            if (File.Exists(imagePath))
            {
                // we are the device that created the image - display it from disk
                MyImage.ImageSource = new FileImageSource(imagePath);
            }
            else
            {
                // this image was created on another device, but it hasn't uploaded it yet
                // to S3. Display a placeholder until the status changes to Remote
                MyImage.ImageSource = placeHolderImage;
            }
            break;
        case DataStatus.Remote:
            MyImage.ImageSource = new ImageSource(recipe.Photo.Url);
            break;
    }
}
```

## Customization

The `RemoteFileManager` is the abstraction that takes care of uploading data to a remote file server. There are three reference implementations in this repo - [`FunctionsFileManager`](https://github.com/nirinchev/realm-lfs/blob/main/Realm.LFS.Functions/FunctionsFileManager.cs) [`S3FileManager`](https://github.com/nirinchev/realm-lfs/blob/main/Realm.LFS.S3/S3FileManager.cs) and [`AzureFileManager`](https://github.com/nirinchev/realm-lfs/blob/main/Realm.LFS.Azure/AzureFileManager.cs). If you want to use your own service, you can use them as inspiration.

## Architecture

The general flow of data in the library is as follows:
1. User creates `FileData` object ([code link](https://github.com/nirinchev/realm-lfs/blob/main/Realm.LFS/FileData.cs#L28))
2. We store the Stream to a file on disk ([code link](https://github.com/nirinchev/realm-lfs/blob/main/Realm.LFS/FileData.cs#L30))
3. We try to upload the file (with incremental backoffs on failure) ([code link](https://github.com/nirinchev/realm-lfs/blob/main/Realm.LFS/FileData.cs#L47))
4. We update the `FileData` with the url of the newly uploaded file ([code link](https://github.com/nirinchev/realm-lfs/blob/main/Realm.LFS/Managers/RemoteFileManager.cs#L161-L181))

The major components are briefly documented below.

### [`FileData`](https://github.com/nirinchev/realm-lfs/blob/main/Realm.LFS/FileData.cs)

This is the RealmObject that holds the metadata associated with the file. It is constructed with a binary source, which it then [persists in a temporary location](https://github.com/nirinchev/realm-lfs/blob/main/Realm.LFS/FileData.cs#L30). When the object is added to Realm, we [schedule the upload](https://github.com/nirinchev/realm-lfs/blob/main/Realm.LFS/FileData.cs#L47).

### [`FileManager`](https://github.com/nirinchev/realm-lfs/blob/main/Realm.LFS/Managers/FileManager.cs)

This is the class that contains most of the orchestration logic and for the most part deals with local files. It exposes internal methods for filesystem manipulation (reading, deleting, etc.), as well as enqueuing files for upload. There are three main file locations:

1. `System`: this is only used for storing placeholders. If the user configures the [placeholder factory](https://github.com/nirinchev/realm-lfs/blob/main/Realm.LFS/Managers/Placeholder.cs#L12), calling [`FileData.GetStream()`](https://github.com/nirinchev/realm-lfs/blob/main/Realm.LFS/FileData.cs#L12) will return a placeholder stream in the case where the file is in status `Local`, but the device doesn't have it stored in the filesystem (this will be the case where the FileData object was synchronized to another device, but the S3 upload hasn't gone through yet and the `FileData.Url` is `null`).
2. `Temporary`: this is where we store all files for `FileData`-s that were created, but have not yet been added to Realm.
3. `Default`: ignore that.
4. `*path-to-realm*/.lfs`: this is where we store all files that are pending upload. When [`FileData.GetStream()`](https://github.com/nirinchev/realm-lfs/blob/main/Realm.LFS/FileData.cs#L12) is called on a FileData with `Status == Local`, this is where we try to find the file from. When `Status == Remote`, this is where we [download the file](https://github.com/nirinchev/realm-lfs/blob/main/Realm.LFS/Managers/FileManager.cs#L50-L53) to (so the client that created the image will almost never have to download it again).

### [`RemoteFileManager`](https://github.com/nirinchev/realm-lfs/blob/main/Realm.LFS/Managers/RemoteFileManager.cs)

This is the class that handles uploads and downloads from a remote file server. Every Realm gets its own file manager and each file manager uploads files to `*hashed-realm-url*/*FileData.Id*`.

The interesting pieces of that class deal with parallelizing uploads. All uploads are enqueued to `_uploadQueue` and picked up by `Executor` instances. If the [size of the queue exceeds twice](https://github.com/nirinchev/realm-lfs/blob/main/Realm.LFS/Managers/RemoteFileManager.cs#L56) the number of executors, a new executor is added up until `MaxExecutors`. Each executor dequeues an item from the queue and executes [`UploadItem`](https://github.com/nirinchev/realm-lfs/blob/main/Realm.LFS/Managers/RemoteFileManager.cs#L150) until the queue is empty, after which it removes itself.

Before each upload, we're checking whether the [item should be uploaded](https://github.com/nirinchev/realm-lfs/blob/main/Realm.LFS/Managers/RemoteFileManager.cs#L130). There are two main cases when this check would return `false`:
1. We're trying to upload an item that was not created on this device (so the file doesn't exist). This could be the case, where we've received a `FileData` from another device with `Status == Local`, the app was killed, then we've [enqueued all pending uploads](https://github.com/nirinchev/realm-lfs/blob/main/Realm.LFS/Managers/RemoteFileManager.cs#L86), which happens on startup). This could obviously be done by filtering the items at time of enqueuing them, but since everything happens on a background thread, it doesn't make much difference.
2. We're trying to upload a local item, that has already been deleted. This could be the case when the user deleted the associated `FileData` before the executors could pick up the item from the queue.

**Note on multithreadedness**: Since Realm doesn't allow accessing objects from multiple threads and we're dealing with a lot of background work here, it's important to take care not to access Realm objects/instances across different threads. This is achieved by using the `BackgroundRunner` class, which dispatches all Realm access on a single thread.

