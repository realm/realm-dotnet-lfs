using System;

namespace Realms.LFS
{
    /// <summary>
    /// The options controlling the <see cref="LFSManager"/> behavior.
    /// </summary>
    public class LFSOptions
    {
        /// <summary>
        /// The location where files will be temporarily stored until they are uploaded.
        /// </summary>
        /// <value>The persistence location for file storage.</value>
        public string? PersistenceLocation { get; init; }

        /// <summary>
        /// A factory for constructing <see cref="RemoteStorageManager"/>. A new manager will be constructed
        /// for each Realm you open.
        /// </summary>
        /// <value>The factory for constructing remote managers.</value>
        public required Func<RealmConfigurationBase, RemoteStorageManager> RemoteManagerFactory { get; init; }
    }
}
