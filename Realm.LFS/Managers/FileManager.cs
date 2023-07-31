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
    private static readonly ConcurrentDictionary<string, RemoteStorageManager> _remoteManagers = new();

    private static string _persistenceLocation = null!;
    private static Func<RealmConfigurationBase, RemoteStorageManager> _remoteManagerFactory = null!;

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
        _persistenceLocation = options.PersistenceLocation ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "realm-lfs");
        _remoteManagerFactory = options.RemoteManagerFactory;

        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        Argument.Ensure(_remoteManagerFactory != null, "A RemoteManagerFactory must be provided.", nameof(options));
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

        return Path.Combine(GetTempPath(), data.Id.ToString());
    }

    internal static void WriteFile(Guid id, Stream stream)
    {
        var filePath = Path.Combine(GetTempPath(), id.ToString());
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

    internal static void UploadFile(FileData data)
    {
        Argument.Ensure(data.IsManaged, "Expected data to be managed.", nameof(data));

        var sourceFile = Path.Combine(GetTempPath(), data.Id.ToString());
        if (File.Exists(sourceFile))
        {
            var targetFile = Path.Combine(GetPath(data.Realm!.Config), data.Id.ToString());
            File.Move(sourceFile, targetFile);
            GetManager(data.Realm.Config).EnqueueUpload(data.Id);
        }
    }

    private static string GetTempPath()
    {
        var folderPath = Path.Combine(_persistenceLocation, "temporary");
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

    private static RemoteStorageManager GetManager(RealmConfigurationBase config)
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