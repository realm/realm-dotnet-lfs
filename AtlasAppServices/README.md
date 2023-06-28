# Setup Atlas App Services App

## Setup SignFunction

The `SignFunction` code is a reference implementation of a function that creates AWS S3 pre-signed urls. It is designed to work alongside with the `Realm.LFS.Functions` package.

### Create the function

1. Go to your Atlas App Services app and navigate to the "Functions" tab. ([Docs](https://www.mongodb.com/docs/atlas/app-services/functions/))
2. Click on "Create New Function".
3. Give it a name, leave everything else as is - the defaults are good enough.
4. Once in the function editor, click "Add Dependency" and add all dependencies listed in `SignFunction/package.json` ([Docs](https://www.mongodb.com/docs/atlas/app-services/functions/dependencies/))
5. Copy and paste the code from `SignFunction/index.js` into the function editor.

### Setup the secrets

1. Go to your Atlas App Services app and navigate to the "Values" tab. ([Docs](https://www.mongodb.com/docs/atlas/app-services/values-and-secrets/))
2. Click on "Create New Value" and add each of the following secrets:
    1. `S3AccessKeyId`: the access key id that will be used to authenticate the S3 client. Needs to have permissions to create pre-signed urls, as well as delete files.
    1. `S3SecretAccessKey`: the secret key that will be used to authenticate the S3 client.
3. Click on "Create New Value" and add each of the following values:
    1. `S3Bucket`: the bucket where your data will be uploaded - e.g. `realm-data-files`.
    2. `S3Region`: the region where the bucket is located - e.g. `us-east-1`.
    3. `S3AccessKeyIdValue`: a value linking to the `S3AccessKeyId` secret.
    4. `S3SecretAccessKeyValue`: a value linking to the `S3SecretAccessKey` secret.

### Explanation

The `SignFunction` processes requests to generate a signed url and handles the 3 operations supported by the SDK:
1. `Upload` generates a pre-signed url with validity of 1 hour to upload data to the S3 bucket. If attempting to override an existing file, an error is returned. The `userId` is stored in the object metadata as that will be used to determine whether the user has permissions to delete the object. If `publicFiles` is set to true, the ACL of the object is set to `public-read`.
2. `Download` returns a download url. If `publicFiles` is set to `true`, the canonical url is returned, otherwise a pre-signed download url is returned.
3. `Delete` deletes an object if its metadata.userId matches the id of the requesting user.