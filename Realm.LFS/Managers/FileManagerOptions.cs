using System;

namespace Realms.LFS
{
    /// <summary>
    /// The options controlling the <see cref="FileManager"/> behavior.
    /// </summary>
    public class FileManagerOptions
    {
        /// <summary>
        /// The location where files will be stored.
        /// </summary>
        public string? PersistenceLocation { get; init; }

        /// <summary>
        /// A factory for constructing <see cref="RemoteFileManager"/>. A new manager will be constructed
        /// for each Realm you open.
        /// </summary>
        public required Func<RealmConfigurationBase, RemoteFileManager> RemoteManagerFactory { get; init; }
    }
}
