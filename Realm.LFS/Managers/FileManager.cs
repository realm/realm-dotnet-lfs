using MongoDB.Bson;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;

namespace Realms.LFS;

/// <summary>
/// This is the class that controls how and where local files are stored.
/// </summary>
public static class FileManager
{
    private static readonly ConcurrentDictionary<string, RemoteFileManager> _remoteManagers = new();

    private static string _persistenceLocation = null!;
    private static Func<RealmConfigurationBase, RemoteFileManager> _remoteManagerFactory = null!;

    /// <summary>
    /// An event invoked whenever a file has been uploaded successfully.
    /// </summary>
    public static event EventHandler<FileUploadedEventArgs>? OnFileUploaded;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileManager"/> class with the supplied options.
    /// </summary>
    /// <param name="options">
    /// Options that control the behavior of the file manager, such as where files are going to be stored, as well
    /// as a factory for constructing remote file managers.
    /// </param>
    public static void Initialize(FileManagerOptions options)
    {
        _persistenceLocation = options.PersistenceLocation ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        _remoteManagerFactory = options.RemoteManagerFactory;
        
        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        Argument.Ensure(_remoteManagerFactory != null, "Either a RemoteManagerFactory or DefaultRemoteManagerFactory must be provided.", nameof(options));
    }

    /// <summary>
    /// Waits for uploads to succeed for the provided Realm.
    /// </summary>
    /// <param name="config">The configuration of the Realm for which to wait for uploads.</param>
    /// <returns>An awaitable task that indicates when uploads are complete.</returns>
    public static Task WaitForUploads(RealmConfigurationBase config)
    {
        return GetManager(config).WaitForUploads();
    }

    internal static Stream? ReadFile(FileLocation location, ObjectId id)
    {
        var path = Path.Combine(GetPath(location), id.ToString());
        return File.Exists(path) ? File.OpenRead(path) : null;
    }

    internal static async Task<Stream?> ReadFile(FileData data)
    {
        var dataPath = GetFilePath(data);
        if (File.Exists(dataPath))
        {
            return File.OpenRead(dataPath);
        }
        
        // If it's supposed to be local but file is missing, we return
        // null since the file is not uploaded by the remote device yet.
        if (data.Status == DataStatus.Local)
        {
            return null;
        }

        var tempPath = dataPath + ".temp";
        await GetManager(data.Realm!.Config).DownloadFile(data, tempPath);

        File.Move(tempPath, dataPath);

        return File.OpenRead(dataPath);
    }

    internal static string GetFilePath(RealmConfigurationBase config, Guid id)
    {
        return Path.Combine(GetPath(config), id.ToString());
    }

    internal static string GetFilePath(FileData data)
    {
        if (data.IsManaged)
        {
            return GetFilePath(data.Realm!.Config, data.Id);
        }

        return Path.Combine(GetPath(FileLocation.Temporary), data.Id.ToString());
    }

    internal static bool FileExists(FileLocation location, string id)
    {
        var path = Path.Combine(GetPath(location), id);
        return File.Exists(path);
    }

    internal static void WriteFile(FileLocation location, Guid id, Stream stream)
    {
        var filePath = Path.Combine(GetPath(location), id.ToString());
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }

        using var fs = File.OpenWrite(filePath);
        if (stream.CanSeek)
        {
            stream.Seek(0, SeekOrigin.Begin);
        }

        stream.CopyTo(fs);
    }

    internal static void CopyFile(FileLocation location, Guid id, string file)
    {
        var targetFile = Path.Combine(GetPath(location), id.ToString());
        File.Copy(file, targetFile, overwrite: true);
    }

    internal static void UploadFile(FileLocation fromLocation, FileData data)
    {
        Argument.Ensure(data.IsManaged, "Expected data to be managed.", nameof(data));

        var sourceFile = Path.Combine(GetPath(fromLocation), data.Id.ToString());
        if (File.Exists(sourceFile))
        {
            var targetFile = Path.Combine(GetPath(data.Realm!.Config), data.Id.ToString());
            File.Move(sourceFile, targetFile);
            GetManager(data.Realm.Config).EnqueueUpload(data.Id);
        }
    }

    private static string GetPath(FileLocation location)
    {
        var folderPath = Path.Combine(_persistenceLocation, location.ToString());
        Directory.CreateDirectory(folderPath);
        return folderPath;
    }

    private static string GetPath(RealmConfigurationBase config)
    {
        var realmFolder = Path.GetDirectoryName(config.DatabasePath)!;
        var folderPath = Path.Combine(realmFolder, ".lfs");
        Directory.CreateDirectory(folderPath);
        return folderPath;
    }

    private static RemoteFileManager GetManager(RealmConfigurationBase config)
    {
        return _remoteManagers.GetOrAdd(config.DatabasePath, _ =>
        {
            var result = _remoteManagerFactory(config);
            result.OnFileUploaded += (s, e) =>
            {
                OnFileUploaded?.Invoke(s, e);
            };

            // TODO: trigger cleanup event with a list of files that can be deleted.

            return result;
        });
    }
}