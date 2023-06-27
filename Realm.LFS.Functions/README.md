# Realm LFS Functions

Realm LFS (large file storage) is an extension of the [Realm.NET SDK](http://github.com/realm/realm-dotnet) that exposes an abstraction for interacting with binary files that are transparently uploaded to a 3rd party service (e.g. S3/Azure Blob Storage) and their URL is subsequently updated in the Realm object for other clients to consume.

This package supplies a `RemoteFileManager` implementation for the [`Realm.LFS`](https://www.nuget.org/packages/Realm.LFS) that uses an Atlas Function to obtain a pre-signed url, which it then uploads the files to.

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

## Atlas Function

This package calls an [Atlas Function](https://www.mongodb.com/docs/atlas/app-services/functions/) to obtain a pre-signed url which it then uploads the data to.

### Function Signature

You can provide your own implementation for the function itself, but it has to have the following signature:

#### Payload

```ts
{
    Operation: "Upload" | "Download" | "Delete",
    FileId: "string"
}
```

The `Operation` field indicates the type of the operation requested - upload, download, or delete the file with id `FileId`.

#### Response

The shape of the response depends on the requested operation.

* Operation: `Upload`:
    ```ts
    {
        Success: true | false,  // Whether the operation completed successfully
        PresignedUrl: "string", // The url to upload the file to
        CanonicalUrl: "string", // The url that can be used to fetch the data from
        Error: "string"         // An error message if the operation failed. Should only be set if Success == false
    }
    ```
* Operation: `Download`:
    ```ts
    {
        Success: true | false,  // Whether the operation completed successfully
        Url: "string",          // The url that contains the file. It can be a presigned url or a normal public url
        Error: "string"         // An error message if the operation failed. Should only be set if Success == false
    }
    ```
* Operation: `Delete`:
    ```ts
    {
        Success: true | false,  // Whether the operation completed successfully
        Error: "string"         // An error message if the operation failed. Should only be set if Success == false
    }
    ```

### Reference implementation

The following is a reference implementation that uses the S3 SDK to generate pre-signed urls:

```js
import { S3Client, PutObjectCommand, DeleteObjectCommand, GetObjectCommand, HeadObjectCommand, NotFound, S3 } from "@aws-sdk/client-s3";
import { getSignedUrl } from "@aws-sdk/s3-request-presigner";

const publicFiles = true;
const validity = 3600;

async function main(payload) {
    const bucket = context.values.get("S3Bucket");
    const accessKeyId = context.values.get("S3AccessKeyIdValue");
    const secretAccessKey = context.values.get("S3SecretAccessKeyValue");
    const region = context.values.get("S3Region");
    const userId = context.user.id;

    const client = new S3Client({
        region,
        credentials: {
            accessKeyId,
            secretAccessKey
        }
    });

    const getCanonicalUrl = (id) => {
        return `https://${bucket}.s3.${region}.amazonaws.com/${id}`;
    }

    const getMetadata = async (id) => {
        try {
            const headCommand = new HeadObjectCommand({ Bucket: bucket, Key: id });
            const response = await client.send(headCommand);
            return response.Metadata || {};
        } catch (err) {
            // There's a bug with the S3 SDK on Atlas Functions - the type hierarchy is messed up and
            // it doesn't have a name property and instanceof NotFound returns false.
            if (`${err}`.indexOf("NotFound") !== -1) {
                return undefined;
            }

            console.log(JSON.stringify(err));
            throw err;
        }
    }

    try {
        switch (payload.Operation) {
            case "Upload":
                const metadata = await getMetadata(payload.FileId);
                if (metadata !== undefined) {
                    return {
                        Success: false,
                        Error: "Object already exists"
                    };
                }

                const uploadCommand = new PutObjectCommand({
                    Bucket: bucket,
                    Key: payload.FileId,
                    ACL: publicFiles ? "public-read" : "private",
                    Metadata: {
                        userid: userId,
                    },
                });

                const uploadUrl = await getSignedUrl(client, uploadCommand, {
                    expiresIn: validity,
                });

                return {
                    Success: true,
                    PresignedUrl: uploadUrl,
                    CanonicalUrl: getCanonicalUrl(payload.FileId),
                };

            case "Download":
                // If files are publicly accessible, there's no need to generate a signed url
                // for the download path.
                if (publicFiles) {
                    return {
                        Success: true,
                        Url: getCanonicalUrl(payload.FileId),
                    };
                }

                const downloadCommand = new GetObjectCommand({ Bucket: bucket, Key: payload.FileId });
                const downloadUrl = await getSignedUrl(client, downloadCommand, { expiresIn: validity });

                return {
                    Success: true,
                    Url: downloadUrl
                };

            case "Delete":
                const deleteMetadata = await getMetadata(payload.FileId);
                if (deleteMetadata === undefined) {
                    return {
                        Success: false,
                        Error: "Object not found"
                    };
                } else if (deleteMetadata.userid !== userId) {
                    return {
                        Success: false,
                        Error: "User issuing the delete needs to match the user that created the request"
                    };
                }

                const deleteCommand = new DeleteObjectCommand({ Bucket: bucket, Key: payload.FileId });
                const response = await client.send(deleteCommand);
                return {
                    Success: response.$metadata.httpStatusCode === 204
                }
            default:
                return {
                    Success: false,
                    Error: `Unknown operation: ${payload.Operation}`
                };
        }
    } catch (err) {
        console.log(`An error occurred executing the function: ${err}`);

        return {
            Success: false,
            Error: "Internal Error. See logs for more details"
        };
    }
}

exports = main;
```

#### Dependencies

This function has the following package dependencies that need to be added via the app services UI:

```json
{
    "@aws-sdk/client-s3": "^3.354.0",
    "@aws-sdk/s3-request-presigner": "^3.354.0"
}
```

#### Secrets

This function uses the following values:

1. `S3Bucket`: the bucket where your data will be uploaded - e.g. `realm-data-files`.
2. `S3Region`: the region where the bucket is located - e.g. `us-east-1`.
3. `S3AccessKeyIdValue`: a value linking to the S3 Access Key Id stored as a secret.
4. `S3SecretAccessKeyValue`: a value linking to the S3 Secret Access Key stored as a secret.