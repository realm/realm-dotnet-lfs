namespace Realms.LFS
{
    /// <summary>
    /// An enum describing the status of a <see cref="FileData"/>.
    /// </summary>
    public enum DataStatus
    {
        /// <summary>
        /// The file is available locally on the device that created the <see cref="FileData"/>.
        /// </summary>
        Local,

        /// <summary>
        /// The file is available remotely and can be accessed by other clients.
        /// </summary>
        Remote,
    }
}
