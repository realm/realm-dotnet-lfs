using System;

namespace Realms.LFS
{
    /// <summary>
    /// The options controlling the <see cref="FileManager"/> behavior.
    /// </summary>
    public class FileManagerOptions
    {
        //Maybe we should specify that is used only for temporary files, until they're moved into the realm folder
        /// <summary>
        /// The location where files will be stored.
        /// </summary>
        /// <value>The persistence location for file storage.</value>
        public string? PersistenceLocation { get; init; }

        /// <summary>
        /// A factory for constructing <see cref="RemoteFileManager"/>. A new manager will be constructed
        /// for each Realm you open.
        /// </summary>
        /// <value>The factory for constructing remote managers.</value>
        public required Func<RealmConfigurationBase, RemoteFileManager> RemoteManagerFactory { get; init; }
    }
}
