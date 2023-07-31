﻿using Microsoft.Azure.Storage;
using Microsoft.Azure.Storage.Blob;
using Microsoft.Azure.Storage.DataMovement;
using System.Threading.Tasks;

namespace Realms.LFS.Azure
{
    /// <summary>
    /// An implementation of the <see cref="RemoteStorageManager"/> that uploads data to Azure.
    /// </summary>
    public class AzureStorageManager : RemoteStorageManager
    {
        private readonly CloudBlobContainer _container;

        /// <summary>
        /// Initializes a new instance of the <see cref="AzureStorageManager"/> class with the
        /// supplied <paramref name="connectionString"/>.
        /// </summary>
        /// <param name="config">The config of the Realm this file manager is tracking.</param>
        /// <param name="connectionString">The connection string used to connect to Azure storage.</param>
        /// <param name="container">
        /// An optional argument indicating the container that will be used to upload data to.
        /// </param>
        public AzureStorageManager(RealmConfigurationBase config, string connectionString, string container = "realm-lfs-data")
            : base(config)
        {
            var account = CloudStorageAccount.Parse(connectionString);
            var client = account.CreateCloudBlobClient();
            _container = client.GetContainerReference(container);
            _container.CreateIfNotExists();
        }

        /// <inheritdoc/>
        protected override async Task DeleteFileCore(string id)
        {
            var blob = _container.GetBlockBlobReference(id);
            await blob.DeleteIfExistsAsync();
        }

        /// <inheritdoc/>
        protected override async Task DownloadFileCore(string id, string file)
        {
            var context = new SingleTransferContext();
            var blob = _container.GetBlockBlobReference(id);
            await TransferManager.DownloadAsync(blob, file, null, context);
        }

        /// <inheritdoc/>
        protected override async Task<string> UploadFileCore(string id, string file)
        {
            var context = new SingleTransferContext();
            var blob = _container.GetBlockBlobReference(id);
            await TransferManager.UploadAsync(file, blob, null, context);
            return blob.Uri.AbsoluteUri;
        }
    }
}
