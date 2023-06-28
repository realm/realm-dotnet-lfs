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
    public Guid Id { get; private set; } = Guid.NewGuid();

    /// <summary>
    /// Gets a stream containing the binary data represented by this <see cref="FileData"/>.
    /// </summary>
    public Task<Stream?> GetStream() => FileManager.ReadFile(this);

    private int StatusInt { get; set; }

    /// <summary>
    /// The <see cref="DataStatus"/> of this <see cref="FileData"/>. This indicates whether the binary
    /// contents have been uploaded to the server or may be available locally.
    /// </summary>
    public DataStatus Status
    {
        get => (DataStatus)StatusInt;
        internal set => StatusInt = (int)value;
    }

    /// <summary>
    /// The local url for this <see cref="FileData"/>. This is the absolute path of the file that contains
    /// the contents of the <see cref="FileData"/>. The file may not exist if the data hasn't been downloaded yet.
    /// </summary>
    public string LocalUrl => FileManager.GetFilePath(this);

    /// <summary>
    /// The remote url of this <see cref="FileData"/>. The value may be <c>null</c> if the file hasn't been uploaded yet.
    /// </summary>
    public string? Url { get; internal set; }

    /// <summary>
    /// The optional name of this <see cref="FileData"/>.
    /// </summary>
    public string? Name { get; private set; }

    /// <summary>
    /// Creates a new instance of the <see cref="FileData"/> class.
    /// </summary>
    /// <param name="data">The stream containing the binary data that will be uploaded.</param>
    /// <param name="name">The name of the file.</param>
    public FileData(Stream data, string? name = null)
    {
        FileManager.WriteFile(FileLocation.Temporary, Id, data);
        Name = name;
        Status = DataStatus.Local;
    }

    partial void OnManaged()
    {
        if (Status == DataStatus.Local)
        {
            // TODO: That's not very efficient - it checks for file existence
            // on every instantiation - we should be able to do it more efficiently 
            FileManager.UploadFile(FileLocation.Temporary, this);
        }
    }

    /// <summary>
    /// Implicitly construct <see cref="FileData"/> from a <c>byte[]</c>.
    /// </summary>
    /// <param name="bytes">
    /// The <c>byte[]</c> that will be wrapped by the <see cref="FileData"/>.
    /// </param>
    public static implicit operator FileData(byte[] bytes) => new(new MemoryStream(bytes));

    /// <summary>
    /// Implicitly construct <see cref="FileData"/> from a <see cref="Stream"/>.
    /// </summary>
    /// <param name="stream">
    /// The <see cref="Stream"/> that will be wrapped by the <see cref="FileData"/>.
    /// </param>
    public static implicit operator FileData(Stream stream) => new(stream);
}
