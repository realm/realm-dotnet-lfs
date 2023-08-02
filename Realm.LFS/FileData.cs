using System;
using MongoDB.Bson;
using System.IO;
using System.Threading.Tasks;

namespace Realms.LFS;

/// <summary>
/// A class wrapping a binary data. This is intended to be used as a drop-in
/// replacement of <c>byte[]</c> properties.
/// </summary>
public partial class FileData : IEmbeddedObject
{
    /// <summary>
    /// Gets the unique id of the <see cref="FileData"/>.
    /// </summary>
    /// <value>The <see cref="FileData"/> Id.</value>
    public Guid Id { get; private set; } = Guid.NewGuid();

    /// <summary>
    /// Gets a stream containing the binary data represented by this <see cref="FileData"/>.
    /// </summary>
    /// <returns>An asynchronous task, that, when resolved, wraps a stream representing the <see cref="FileData"/>.</returns>
    /// <remarks>
    /// If the <see cref="Status"/> of the <see cref="FileData"/> is <see cref="DataStatus.Local"/>, the stream will contain
    /// the local file if the <see cref="FileData"/> was created by the current device and the file is available. If the
    /// type is <see cref="DataStatus.Local"/> but the file was uploaded by a different client, and thus not yet synchronized,
    /// <c>null</c> will be returned. Finally, if <see cref="Status"/> is <see cref="DataStatus.Remote"/>, the file will be
    /// downloaded locally before a stream is returned.
    /// </remarks>
    public Task<Stream?> GetStream() => LFSManager.ReadFile(this);

    private int StatusInt { get; set; }

    /// <summary>
    /// The <see cref="DataStatus"/> of this <see cref="FileData"/>. This indicates whether the binary
    /// contents have been uploaded to the server or may be available locally.
    /// </summary>
    /// <value>The <see cref="FileData"/> status.</value>
    public DataStatus Status
    {
        get => (DataStatus)StatusInt;
        internal set => StatusInt = (int)value;
    }

    /// <summary>
    /// The local url for this <see cref="FileData"/>. This is the absolute path of the file that contains
    /// the contents of the <see cref="FileData"/>. The file may not exist if the data hasn't been downloaded yet.
    /// </summary>
    /// <value>The local path to the file data contents.</value>
    public string LocalUrl => LFSManager.GetFilePath(this);

    /// <summary>
    /// The remote url of this <see cref="FileData"/>. The value may be <c>null</c> if the file hasn't been uploaded yet.
    /// </summary>
    /// <value>The <see cref="FileData"/> remote url.</value>
    public string? Url { get; internal set; }

    /// <summary>
    /// The optional name of this <see cref="FileData"/>.
    /// </summary>
    /// <value>The <see cref="FileData"/> name.</value>
    public string? Name { get; private set; }

    /// <summary>
    /// Creates a new instance of the <see cref="FileData"/> class.
    /// </summary>
    /// <param name="data">The stream containing the binary data that will be uploaded.</param>
    /// <param name="name">The name of the file.</param>
    public FileData(Stream data, string? name = null)
    {
        LFSManager.WriteFile(Id, data);
        Name = name;
        Status = DataStatus.Local;
    }

    partial void OnManaged()
    {
        if (Status == DataStatus.Local)
        {
            // TODO: That's not very efficient - it checks for file existence
            // on every instantiation - we should be able to do it more efficiently 
            LFSManager.UploadFile(this);
        }
    }

    /// <summary>
    /// Implicitly construct <see cref="FileData"/> from a <c>byte[]</c>.
    /// </summary>
    /// <param name="bytes">
    /// The <c>byte[]</c> that will be wrapped by the <see cref="FileData"/>.
    /// </param>
    /// <returns>A <see cref="FileData"/> instance wrapping the supplied byte array.</returns>
    public static implicit operator FileData(byte[] bytes) => new(new MemoryStream(bytes));

    /// <summary>
    /// Implicitly construct <see cref="FileData"/> from a <see cref="Stream"/>.
    /// </summary>
    /// <param name="stream">
    /// The <see cref="Stream"/> that will be wrapped by the <see cref="FileData"/>.
    /// </param>
    /// <returns>A <see cref="FileData"/> instance wrapping the supplied stream.</returns>
    public static implicit operator FileData(Stream stream) => new(stream);
}
