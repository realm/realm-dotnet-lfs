using MongoDB.Bson;
using System;
using System.IO;

namespace Realms.LFS
{
    /// <summary>
    /// The event args describing that a file has been successfully uploaded by the
    /// <see cref="RemoteFileManager"/>.
    /// </summary>
    public class FileUploadedEventArgs : EventArgs
    {
        /// <summary>
        /// The id of the <see cref="FileData"/> object that has been uploaded.
        /// </summary>
        public Guid FileDataId { get; }

        /// <summary>
        /// The file path of the local file.
        /// </summary>
        /// <value>The path to the uploaded file.</value>
        public string FilePath { get; }

        /// <summary>
        /// The Realm configuration for the Realm owning the file.
        /// </summary>
        /// <value>The configuration of the owning Realm.</value>
        public RealmConfigurationBase RealmConfig { get; }

        /// <summary>
        /// Deletes the local copy if you no longer need it.
        /// </summary>
        /// <remarks>
        /// This can be useful if you generally rarely need to access the files after they've been uploaded. The file
        /// can still be downloaded from the remote url if necessary.
        /// </remarks>
        public void DeleteLocalCopy()
        {
            File.Delete(FilePath);
        }

        internal FileUploadedEventArgs(Guid id, string filePath, RealmConfigurationBase realmConfig)
        {
            FileDataId = id;
            FilePath = filePath;
            RealmConfig = realmConfig;
        }
    }
}
