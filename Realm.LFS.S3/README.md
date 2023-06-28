# Realm LFS Azure

Realm LFS (large file storage) is an extension of the [Realm.NET SDK](http://github.com/realm/realm-dotnet) that exposes an abstraction for interacting with binary files that are transparently uploaded to a 3rd party service (e.g. S3/Azure Blob Storage) and their URL is subsequently updated in the Realm object for other clients to consume.

This package supplies a `RemoteFileManager` implementation for the [`Realm.LFS`](https://www.nuget.org/packages/Realm.LFS) that uploads the files to S3.

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
var credentials = new AWSCredentials(...);
FileManager.Initialize(new FileManagerOptions
{
    RemoteManagerFactory = (config) => new S3FileManager(config, credentials)
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
                // to Blob Storage. Display a placeholder until the status changes to Remote
                MyImage.ImageSource = placeHolderImage;
            }
            break;
        case DataStatus.Remote:
            MyImage.ImageSource = new ImageSource(recipe.Photo.Url);
            break;
    }
}
```