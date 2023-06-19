using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;
using System.Threading.Tasks;

namespace Realms.LFS.S3
{
    /// <summary>
    /// An implementation of the <see cref="RemoteFileManager"/> that uploads data to AWS S3.
    /// </summary>
    public class S3FileManager : RemoteFileManager
    {
        private readonly AmazonS3Client _s3Client;
        private readonly string _bucket;

        /// <summary>
        /// Initializes a new instance of the <see cref="S3FileManager"/> class with the
        /// supplied <paramref name="credentials"/>.
        /// </summary>
        /// <param name="config">The config of the Realm this file manager is tracking.</param>
        /// <param name="credentials">The credentials used to connect to the S3 bucket.</param>
        /// <param name="region">The region where the bucket is located</param>
        /// <param name="bucket">
        /// An optional argument indicating the bucket that will be used to upload data to.
        /// </param>
        public S3FileManager(RealmConfigurationBase config, AWSCredentials credentials, RegionEndpoint region, string bucket = "realm-lfs-data")
            : base(config)
        {
            _s3Client = new AmazonS3Client(credentials, region);
            _bucket = bucket;
        }

        /// <inheritdoc/>
        protected override async Task DeleteFileCore(string id)
        {
            await _s3Client.DeleteObjectAsync(new DeleteObjectRequest
            {
                BucketName = _bucket,
                Key = id
            });
        }

        /// <inheritdoc/>
        protected override async Task DownloadFileCore(string id, string file, FileData _)
        {
            var fileTransferUtility = new TransferUtility(_s3Client);
            await fileTransferUtility.DownloadAsync(new TransferUtilityDownloadRequest
            {
                Key = id,
                BucketName = _bucket,
                FilePath = file,
            });
        }

        /// <inheritdoc/>
        protected override async Task<string> UploadFileCore(string id, string file)
        {
            var fileTransferUtility = new TransferUtility(_s3Client);

            var fileTransferUtilityRequest = new TransferUtilityUploadRequest
            {
                BucketName = _bucket,
                FilePath = file,
                Key = id,
                StorageClass = S3StorageClass.StandardInfrequentAccess,
                CannedACL = S3CannedACL.PublicRead,
            };

            await fileTransferUtility.UploadAsync(fileTransferUtilityRequest);

            return $"https://{_bucket}.s3.amazonaws.com/{id}";
        }
    }
}
