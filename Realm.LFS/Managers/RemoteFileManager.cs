using MongoDB.Bson;
using Realms.Logging;
using Realms.Sync;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Realms.LFS
{
    /// <summary>
    /// The base class for any remote file manager implementation. This will be used to upload local files
    /// to a remote storage provider, such as S3 or Azure.
    /// </summary>
    public abstract class RemoteFileManager
    {
        internal event EventHandler<FileUploadedEventArgs>? OnFileUploaded;

        private const int MaxRetryDelay = 60000;
        private const int MinRetryDelay = 100;
        private const int MaxExecutors = 10;

        private readonly ConcurrentQueue<UploadDetails> _uploadQueue = new();
        private readonly ExecutorList<UploadDetails> _executors;

        private readonly BackgroundRunner _runner;
        private readonly string _realmPathHash;

        /// <summary>
        /// The config of the Realm this manager tracks. 
        /// </summary>
        protected RealmConfigurationBase RealmConfig { get; }

        /// <summary>
        /// Constructs a new <see cref="RemoteFileManager"/>.
        /// </summary>
        protected RemoteFileManager(RealmConfigurationBase config)
        {
            RealmConfig = config;

            _executors = new(MaxExecutors, _uploadQueue, UploadItem);

            var realmPath = RealmConfig switch
            {
                FlexibleSyncConfiguration flxConfig => flxConfig.User.Id,
                PartitionSyncConfiguration pbsConfig => $"{pbsConfig.User.Id}{pbsConfig.Partition}",
                _ => Path.GetFileNameWithoutExtension(RealmConfig.DatabasePath)
            };

            _realmPathHash = HashHelper.MD5(realmPath);

            _runner = new BackgroundRunner(RealmConfig);
            EnqueueExisting();
        }

        internal void EnqueueUpload(ObjectId dataId, int retryAfter = MinRetryDelay)
        {
            _uploadQueue.Enqueue(new UploadDetails(dataId, retryAfter));
            _executors.AddIfNecessary();
        }

        internal Task DownloadFile(FileData data, string destinationFile)
        {
            Argument.Ensure(data.Status == DataStatus.Remote, $"Expected remote data, got {data.Status}", nameof(data));

            return DownloadFileCore(GetRemoteId(data.Id), destinationFile, data);
        }

        internal Task WaitForUploads() => _executors.WaitForCompletion();

        /// <summary>
        /// Uploads a file with the specified <paramref name="remoteId"/> and path.
        /// </summary>
        /// <param name="remoteId">The id of the file.</param>
        /// <param name="file">The absolute path to the file on the local filesystem.</param>
        /// <returns>
        /// A Task wrapping the remote service url where the file will be accessible via http calls.
        /// </returns>
        protected abstract Task<string> UploadFileCore(string remoteId, string file);

        /// <summary>
        /// Downloads a file with the specified <paramref name="remoteId"/> and name.
        /// </summary>
        /// <param name="remoteId">The remote id of the file.</param>
        /// <param name="file">The absolute path to the file on the local filesystem.</param>
        /// <param name="data">The <see cref="FileData"/> wrapping the file.</param>
        /// <returns>A Task wrapping the download operation.</returns>
        protected abstract Task DownloadFileCore(string remoteId, string file, FileData data);

        /// <summary>
        /// Deletes a file with the specified <paramref name="remoteId"/> from the remote service.
        /// </summary>
        /// <param name="remoteId">The id of the file.</param>
        /// <returns>A Task wrapping the delete operation</returns>
        protected abstract Task DeleteFileCore(string remoteId);

        private void EnqueueExisting()
        {
            _runner.Execute((realm) =>
            {
                var unprocessedDatas = GetFileDatas(realm).Filter($"StatusInt == {(int)DataStatus.Local}");
                foreach (var item in unprocessedDatas)
                {
                    EnqueueUpload(item.Id);
                }
            });
        }

        private async Task<bool> ShouldUpload(UploadDetails details)
        {
            var filePath = FileManager.GetFilePath(RealmConfig, details.DataId);
            if (!File.Exists(filePath))
            {
                return false;
            }

            return await _runner.Execute(realm =>
            {
                var data = GetFileData(realm, details.DataId);
                if (data == null)
                {
                    realm.Refresh();
                    data = GetFileData(realm, details.DataId);
                }
                
                return data?.Status == DataStatus.Local;
            });
        }

        private async Task UploadItem(UploadDetails details)
        {
            if (!await ShouldUpload(details))
            {
                return;
            }

            try
            {
                var filePath = FileManager.GetFilePath(RealmConfig, details.DataId);
                var url = await UploadFileCore(GetRemoteId(details.DataId), filePath);
                var success = await _runner.Execute((realm) =>
                {
                    using var transaction = realm.BeginWrite();
                    var data = GetFileData(realm, details.DataId);

                    if (data == null)
                    {
                        transaction.Rollback();
                        return false;
                    }

                    data.Url = url;
                    data.Status = DataStatus.Remote;
                    transaction.Commit();

                    return true;
                });

                if (success)
                {
                    OnFileUploaded?.Invoke(this, new FileUploadedEventArgs(details.DataId, filePath, RealmConfig));
                }
                else
                {
                    Logger.Default.Log(LogLevel.Error, $"Realm.LFS: Could not find data with Id: {details.DataId}");
                    await DeleteFileCore(GetRemoteId(details.DataId));
                }
            }
            catch (Exception ex)
            {
                Logger.Default.Log(LogLevel.Error, $"Realm.LFS: An error occurred while uploading item: {ex}");
                _ = Task.Delay(details.RetryAfter).ContinueWith(_ =>
                {
                    EnqueueUpload(details.DataId, Math.Min(details.RetryAfter * 2, MaxRetryDelay));
                });
            }
        }

        private string GetRemoteId(ObjectId dataId) => $"{_realmPathHash}/{dataId}";

        private class UploadDetails
        {
            public ObjectId DataId { get; }
            public int RetryAfter { get; }

            public UploadDetails(ObjectId dataId, int retryAfter)
            {
                DataId = dataId;
                RetryAfter = retryAfter;
            }
        }

        private static IQueryable<FileData> GetFileDatas(Realm realm)
        {
            var result = typeof(Realm).GetMethod("AllEmbedded", BindingFlags.NonPublic | BindingFlags.Instance)!
                .MakeGenericMethod(typeof(FileData))
                .Invoke(realm, null);

            return (IQueryable<FileData>)result;
        }

        private static FileData? GetFileData(Realm realm, ObjectId id) =>
            GetFileDatas(realm).FirstOrDefault(d => d.Id == id);
    }
}
