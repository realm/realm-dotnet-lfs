using System;

namespace Realms.LFS
{
    /// <summary>
    /// The options controlling the <see cref="FileManager"/> behavior.
    /// </summary>
    public class FileManagerOptions
    {
        /// <summary>
        /// A placeholder to be returned for images where we haven't downloaded the remote file yet. 
        /// </summary>
        public Placeholder Placeholder { get; set; }

        /// <summary>
        /// The location where files will be stored.
        /// </summary>
        public string PersistenceLocation { get; set; }

        /// <summary>
        /// A factory for constructing <see cref="RemoteFileManager"/>. A new manager will be constructed
        /// for each Realm you open.
        /// </summary>
        public Func<RemoteFileManager> RemoteManagerFactory { get; set; }
    }
}
