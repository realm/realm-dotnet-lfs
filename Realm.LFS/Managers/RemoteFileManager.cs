using MongoDB.Bson;
using Realms.Logging;
using Realms.Sync;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Realms.LFS
{
    /// <summary>
    /// The base class for any remote file manager implementation. This will be used to upload local files
    /// to a remote storage provider, such as S3 or Azure.
    /// </summary>
    public abstract class RemoteFileManager
    {
        internal event EventHandler<FileUploadedEventArgs> OnFileUploaded;

        private const int MaxRetryDelay = 60000;
        private const int MinRetryDelay = 100;
        private const int MaxExecutors = 10;

        private readonly ConcurrentQueue<UploadDetails> _uploadQueue = new();
        private readonly List<Executor<UploadDetails>> _executors = new();
        private readonly object _executorLock = new();

        private BackgroundRunner _runner;
        private RealmConfigurationBase _config;
        private string _realmPathHash;
        private TaskCompletionSource<object> _completionTcs;

        /// <summary>
        /// Constructs a new <see cref="RemoteFileManager"/>.
        /// </summary>
        protected RemoteFileManager()
        {
        }

        internal void Start(RealmConfigurationBase config)
        {
            _config = config;

            var realmPath = _config switch
            {
                FlexibleSyncConfiguration flxConfig => flxConfig.User.Id,
                PartitionSyncConfiguration pbsConfig => pbsConfig.User.Id + pbsConfig.Partition.ToString(),
                _ => Path.GetFileNameWithoutExtension(_config.DatabasePath)
            };

            _realmPathHash = HashHelper.MD5(realmPath);

            _runner = new BackgroundRunner(_config);
            EnqueueExisting();
        }

        internal void EnqueueUpload(ObjectId dataId, int retryAfter = MinRetryDelay)
        {
            _uploadQueue.Enqueue(new UploadDetails(dataId, retryAfter));

            var executorCount = _executors.Count;
            if (_uploadQueue.Count > 2 * executorCount && executorCount < MaxExecutors)
            {
                AddExecutor();
            }
        }

        internal Task DownloadFile(FileData data, string destinationFile)
        {
            Argument.Ensure(data.Status == DataStatus.Remote, $"Expected remote data, got {data.Status}", nameof(data));

            return DownloadFileCore(GetRemoteId(data.Id), destinationFile);
        }

        internal Task WaitForUploads() => _completionTcs?.Task ?? Task.CompletedTask;

        /// <summary>
        /// Uploads a file with the specified <paramref name="id"/> and path.
        /// </summary>
        /// <param name="id">The id of the file.</param>
        /// <param name="file">The absolute path to the file on the local filesystem.</param>
        /// <returns>
        /// A Task wrapping the remote service url where the file will be accessible via http calls.
        /// </returns>
        protected abstract Task<string> UploadFileCore(string id, string file);

        /// <summary>
        /// Downloads a file with the specified <paramref name="id"/> and name.
        /// </summary>
        /// <param name="id">The id of the file.</param>
        /// <param name="file">The absolute path to the file on the local filesystem.</param>
        /// <returns>A Task wrapping the download operation.</returns>
        protected abstract Task DownloadFileCore(string id, string file);

        /// <summary>
        /// Deletes a file with the specified <paramref name="id"/> from the remote service.
        /// </summary>
        /// <param name="id">The id of the file.</param>
        /// <returns>A Task wrapping the delete operation</returns>
        protected abstract Task DeleteFileCore(string id);

        private void EnqueueExisting()
        {
            _runner.Execute((realm) =>
            {
                var unprocessedDatas = realm.All<FileData>().Filter($"StatusInt == {(int)DataStatus.Local}");
                foreach (var item in unprocessedDatas)
                {
                    EnqueueUpload(item.Id);
                }
            });
        }

        private void AddExecutor()
        {
            lock (_executorLock)
            {
                if (_executors.Count >= MaxExecutors)
                {
                    return;
                }

                var executor = new Executor<UploadDetails>(_uploadQueue, UploadItem, RemoveExecutor);
                _executors.Add(executor);

                if (_executors.Count == 1)
                {
                    _completionTcs = new TaskCompletionSource<object>();
                }
            }
        }

        private void RemoveExecutor(Executor<UploadDetails> executor)
        {
            lock (_executorLock)
            {
                _executors.Remove(executor);

                if (_executors.Count == 0)
                {
                    _completionTcs.TrySetResult(null);
                }
            }
        }

        private async Task<bool> ShouldUpload(UploadDetails details)
        {
            var filePath = FileManager.GetFilePath(_config, details.DataId);
            if (!File.Exists(filePath))
            {
                return false;
            }

            return await _runner.Execute(realm =>
            {
                var data = realm.Find<FileData>(details.DataId);
                if (data == null)
                {
                    realm.Refresh();
                    data = realm.Find<FileData>(details.DataId);
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
                var filePath = FileManager.GetFilePath(_config, details.DataId);
                var url = await UploadFileCore(GetRemoteId(details.DataId), filePath);
                var success = await _runner.Execute((realm) =>
                {
                    using var transaction = realm.BeginWrite();
                    var data = realm.Find<FileData>(details.DataId);

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
                    OnFileUploaded?.Invoke(this, new FileUploadedEventArgs
                    {
                        FileDataId = details.DataId,
                        FilePath = filePath,
                        RealmConfig = _config,
                    });
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
    }
}
