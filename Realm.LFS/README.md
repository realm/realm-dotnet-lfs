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

There are 3 supplied implementations of `RemoteFileManager`, each shipped in a separate package:
1. [`Realm.LFS.Functions`](https://www.nuget.org/packages/Realm.LFS.Functions) provides a file manager that calls an Atlas Function to obtain a pre-signed url. Then it uploads data to the retried url.
2. [`Realm.LFS.S3`](https://www.nuget.org/packages/Realm.LFS.S3) uses the S3 SDK to upload files to an S3 bucket.
3. [`Realm.LFS.Azure`](https://www.nuget.org/packages/Realm.LFS.Azure) uses the Azure SDK to upload files to Azure Blob Storage.

If you use another service or have your own web server that can process the file uploads, you need to supply your own implementation of `RemoteFileManager`.