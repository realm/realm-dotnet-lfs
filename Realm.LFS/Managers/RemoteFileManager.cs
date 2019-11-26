using Realms.Helpers;
using Realms.Sync;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Realms.LFS
{
    public abstract class RemoteFileManager
    {
        internal event EventHandler<FileUploadedEventArgs> OnFileUploaded;

        private const int MaxRetryDelay = 60000;
        private const int MinRetryDelay = 100;
        private const int MaxExecutors = 10;

        private readonly ConcurrentQueue<UploadDetails> _uploadQueue = new ConcurrentQueue<UploadDetails>();
        private readonly List<Executor<UploadDetails>> _executors = new List<Executor<UploadDetails>>();
        private readonly object _executorLock = new object();

        private BackgroundRunner _runner;
        private RealmConfigurationBase _config;
        private string _realmPathHash;
        private TaskCompletionSource<object> _completionTcs;

        protected RemoteFileManager()
        {
        }

        internal void Start(RealmConfigurationBase config)
        {
            _config = config;
            string realmPath;
            if (_config is SyncConfigurationBase syncConfig)
            {
                realmPath = syncConfig.ServerUri.PathAndQuery.Replace("~", syncConfig.User.Identity);
            }
            else
            {
                realmPath = Path.GetFileNameWithoutExtension(_config.DatabasePath);
            }
            _realmPathHash = HashHelper.MD5(realmPath);


            _runner = new BackgroundRunner(_config);
            EnqueueExisting();
        }

        internal void EnqueueUpload(string dataId, int retryAfter = MinRetryDelay)
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

            return DownloadFileCore(GetId(data.Id), destinationFile);
        }

        internal Task WaitForUploads()
        {
            var tcs = _completionTcs;
            if (tcs == null)
            {
                return Task.FromResult<object>(null);
            }

            return tcs.Task;
        }

        protected abstract Task<string> UploadFileCore(string id, string file);

        protected abstract Task DownloadFileCore(string id, string file);

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
                return data != null && data.Status == DataStatus.Local;
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
                Console.WriteLine($"Uploading on {Environment.CurrentManagedThreadId}");
                var url = await UploadFileCore(GetId(details.DataId), filePath);
                Console.WriteLine($"Updating Realm on {Environment.CurrentManagedThreadId}");
                var success = await _runner.Execute((realm) =>
                {
                    using (var transaction = realm.BeginWrite())
                    {
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
                    }
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
                    Logger.Error($"Could not find data with Id: {details.DataId}");
                    await DeleteFileCore(details.DataId);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex.Message);
                _ = Task.Delay(details.RetryAfter).ContinueWith(_ =>
                {
                    EnqueueUpload(details.DataId, Math.Min(details.RetryAfter * 2, MaxRetryDelay));
                });
            }
        }

        private string GetId(string dataId) => $"{_realmPathHash}/{dataId}";

        private class UploadDetails
        {
            public string DataId { get; }
            public int RetryAfter { get; }

            public UploadDetails(string dataId, int retryAfter)
            {
                DataId = dataId;
                RetryAfter = retryAfter;
            }
        }
    }
}
