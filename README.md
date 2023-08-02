# Realm LFS

Realm LFS (large file storage) is an extension of the [Realm.NET SDK](http://github.com/realm/realm-dotnet) that exposes an abstraction for interacting with binary files that are transparently uploaded to a 3rd party service (e.g. S3/Azure Blob Storage) and their URL is subsequently updated in the Realm object for other clients to consume.

## Background

Using binary data (i.e. `byte[]` properties) with Realm is supported but very inefficient. The recommended approach is to upload the data to a file hosting service and store just the URL in the Realm object, but doing so requires internet connection and defeats one of the main advantages of using Realm. The purpose of this library is to abstract as much as possible the uploading part and expose an interface that is as close as possible to the Realm API of interacting with data.

## Usage

To initialize the SDK, the minimum configuration you need to do is to configure the remote manager factory:

```csharp
LFSManager.Initialize(new LFSOptions
{
    RemoteManagerFactory = (config) => new AtlasFunctionsStorageManager(config, "MyDataFunction")
});
```

Then, replace `byte[]` properties with `FileData` ones:

```diff
public class Recipe : RealmObject
{
    public string Name { get; set; }

    public string Summary { get; set; }

    public IList<Ingredient> Ingredients { get; set; }

-    public byte[] Photo { get; set; }
+    public FileData Photo { get; set; }
}
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

The `RemoteStorageManager` is the abstraction that takes care of uploading data to a remote file server. There are three reference implementations in this repo - [`AtlasFunctionsStorageManager`](https://github.com/realm/realm-dotnet-lfs/blob/main/Realm.LFS.Functions/AtlasFunctionsStorageManager.cs) [`S3StorageManager`](https://github.com/realm/realm-dotnet-lfs/blob/main/Realm.LFS.S3/S3StorageManager.cs) and [`AzureStorageManager`](https://github.com/realm/realm-dotnet-lfs/blob/main/Realm.LFS.Azure/AzureStorageManager.cs). If you want to use your own service, you can use them as inspiration.

## Documentation

API docs can be found at https://realm.github.io/realm-dotnet-lfs/.

## Architecture

[`Architecture.md`](https://github.com/realm/realm-dotnet-lfs/blob/main/Architecture.md) contains an overview of the library architecture.