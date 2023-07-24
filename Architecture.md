# Architecture

The general flow of data in the library is as follows:
1. User creates `FileData` object ([code link](https://github.com/realm/realm-dotnet-lfs/blob/630b323b31e45a25fee9fac4bf745c8b9123c34a/Realm.LFS/FileData.cs#L57))
2. We store the Stream to a file on disk ([code link](https://github.com/realm/realm-dotnet-lfs/blob/630b323b31e45a25fee9fac4bf745c8b9123c34a/Realm.LFS/FileData.cs#L59))
3. We try to upload the file (with incremental backoffs on failure) ([code link](https://github.com/realm/realm-dotnet-lfs/blob/630b323b31e45a25fee9fac4bf745c8b9123c34a/Realm.LFS/FileData.cs#L70))
4. We update the `FileData` with the url of the newly uploaded file ([code link](https://github.com/realm/realm-dotnet-lfs/blob/630b323b31e45a25fee9fac4bf745c8b9123c34a/Realm.LFS/Managers/RemoteFileManager.cs#L143-L156))

The major components are briefly documented below.

## [`FileData`](https://github.com/realm/realm-dotnet-lfs/blob/main/Realm.LFS/FileData.cs)

This is the RealmObject that holds the metadata associated with the file. It is constructed with a binary source, which it then [persists in a temporary location](https://github.com/realm/realm-dotnet-lfs/blob/630b323b31e45a25fee9fac4bf745c8b9123c34a/Realm.LFS/FileData.cs#L59). When the object is added to Realm, we [schedule the upload](https://github.com/realm/realm-dotnet-lfs/blob/630b323b31e45a25fee9fac4bf745c8b9123c34a/Realm.LFS/FileData.cs#L70).

## [`FileManager`](https://github.com/realm/realm-dotnet-lfs/blob/main/Realm.LFS/Managers/FileManager.cs)

This is the class that contains most of the orchestration logic and for the most part deals with local files. It exposes internal methods for filesystem manipulation (reading, deleting, etc.), as well as enqueuing files for upload. There are two main file locations:

1. `Temporary`: this is where we store all files for `FileData`-s that were created, but have not yet been added to Realm.
3. `*path-to-realm*/.lfs`: this is where we store all files that are pending upload. When [`FileData.GetStream()`](https://github.com/realm/realm-dotnet-lfs/blob/630b323b31e45a25fee9fac4bf745c8b9123c34a/Realm.LFS/FileData.cs#L22) is called on a FileData with `Status == Local`, this is where we try to find the file from. When `Status == Remote`, this is where we [download the file](https://github.com/realm/realm-dotnet-lfs/blob/630b323b31e45a25fee9fac4bf745c8b9123c34a/Realm.LFS/Managers/FileManager.cs#L50-L54) to (so the client that created the image will almost never have to download it again).

## [`RemoteFileManager`](https://github.com/realm/realm-dotnet-lfs/blob/main/Realm.LFS/Managers/RemoteFileManager.cs)

This is the class that handles uploads and downloads from a remote file server. Every Realm gets its own file manager and each file manager uploads files to `*hashed-realm-url*/*FileData.Id*`.

The interesting pieces of that class deal with parallelizing uploads. All uploads are enqueued to `_uploadQueue` and picked up by `Executor` instances. If the [size of the queue exceeds twice](https://github.com/realm/realm-dotnet-lfs/blob/630b323b31e45a25fee9fac4bf745c8b9123c34a/Realm.LFS/Helpers/ExecutorList.cs#L25-L42) the number of executors, a new executor is added up until `MaxExecutors`. Each executor dequeues an item from the queue and executes [`UploadItem`](https://github.com/realm/realm-dotnet-lfs/blob/630b323b31e45a25fee9fac4bf745c8b9123c34a/Realm.LFS/Managers/RemoteFileManager.cs#L140) until the queue is empty, after which it removes itself.

Before each upload, we're checking whether the [item should be uploaded](https://github.com/realm/realm-dotnet-lfs/blob/630b323b31e45a25fee9fac4bf745c8b9123c34a/Realm.LFS/Managers/RemoteFileManager.cs#L132-L135). There are two main cases when this check would return `false`:
1. We're trying to upload an item that was not created on this device (so the file doesn't exist). This could be the case, where we've received a `FileData` from another device with `Status == Local`, the app was killed, then we've [enqueued all pending uploads](https://github.com/realm/realm-dotnet-lfs/blob/630b323b31e45a25fee9fac4bf745c8b9123c34a/Realm.LFS/Managers/RemoteFileManager.cs#L97), which happens on startup). This could obviously be done by filtering the items at time of enqueuing them, but since everything happens on a background thread, it doesn't make much difference.
2. We're trying to upload a local item, that has already been deleted. This could be the case when the user deleted the associated `FileData` before the executors could pick up the item from the queue.

**Note on multithreadedness**: Since Realm doesn't allow accessing objects from multiple threads and we're dealing with a lot of background work here, it's important to take care not to access Realm objects/instances across different threads. This is achieved by using the `BackgroundRunner` class, which dispatches all Realm access on a single thread.

