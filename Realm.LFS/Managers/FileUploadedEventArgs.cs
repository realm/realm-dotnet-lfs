using System;
using System.IO;
using MongoDB.Bson;

namespace Realms.LFS
{
    public class FileUploadedEventArgs : EventArgs
    {
        public ObjectId FileDataId { get; internal set; }

        public string FilePath { get; internal set; }

        public RealmConfigurationBase RealmConfig { get; internal set; }

        public void DeleteLocalCopy()
        {
            File.Delete(FilePath);
        }

        internal FileUploadedEventArgs() { }
    }
}
