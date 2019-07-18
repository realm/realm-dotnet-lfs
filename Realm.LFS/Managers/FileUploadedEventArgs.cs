using System;
using System.IO;

namespace Realms.LFS
{
    public class FileUploadedEventArgs : EventArgs
    {
        public string FileDataId { get; internal set; }

        public string FilePath { get; internal set; }

        public RealmConfigurationBase RealmConfig { get; internal set; }

        public void DeleteLocalCopy()
        {
            File.Delete(FilePath);
        }

        internal FileUploadedEventArgs() { }
    }
}
