using Microsoft.Azure.Storage;
using Microsoft.Azure.Storage.Blob;
using Microsoft.Azure.Storage.DataMovement;
using System.Threading.Tasks;

namespace Realms.LFS.Azure
{
    public class AzureFileManager : RemoteFileManager
    {
        private readonly CloudBlobContainer _container;
        public AzureFileManager(string connectionString, string container = "realm-lfs-data")
        {
            var account = CloudStorageAccount.Parse(connectionString);
            var client = account.CreateCloudBlobClient();
            _container = client.GetContainerReference(container);
            _container.CreateIfNotExists();
        }

        protected override async Task DeleteFileCore(string id)
        {
            var blob = _container.GetBlockBlobReference(id);
            await blob.DeleteIfExistsAsync();
        }

        protected override async Task DownloadFileCore(string id, string file)
        {
            var context = new SingleTransferContext();
            var blob = _container.GetBlockBlobReference(id);
            await TransferManager.DownloadAsync(blob, file, null, context);
        }

        protected override async Task<string> UploadFileCore(string id, string file)
        {
            var context = new SingleTransferContext();
            var blob = _container.GetBlockBlobReference(id);
            await TransferManager.UploadAsync(file, blob, null, context);
            return blob.Uri.AbsoluteUri;
        }
    }
}
