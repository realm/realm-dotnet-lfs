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
        public ObjectId FileDataId { get; }

        /// <summary>
        /// The file path of the local file.
        /// </summary>
        public string FilePath { get; }

        /// <summary>
        /// The Realm configuration for the Realm owning the file.
        /// </summary>
        public RealmConfigurationBase RealmConfig { get; }

        /// <summary>
        /// A method that allows you to delete the local copy if you no longer need it.
        /// </summary>
        public void DeleteLocalCopy()
        {
            File.Delete(FilePath);
        }

        internal FileUploadedEventArgs(ObjectId id, string filePath, RealmConfigurationBase realmConfig)
        {
            FileDataId = id;
            FilePath = filePath;
            RealmConfig = realmConfig;
        }
    }
}
