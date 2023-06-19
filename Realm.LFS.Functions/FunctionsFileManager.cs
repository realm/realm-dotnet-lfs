using System.Diagnostics.CodeAnalysis;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Realms.Sync;

// ReSharper disable ClassNeverInstantiated.Local
// ReSharper disable UnusedAutoPropertyAccessor.Local

namespace Realms.LFS;

/// <summary>
/// An implementation of the <see cref="RemoteFileManager"/> that uploads data to a url supplied
/// by an Atlas App Services function.
/// </summary>
public class FunctionsFileManager : RemoteFileManager
{
    private readonly string _function;
    private readonly HttpClient _client;
    private readonly User _user;
    
    public FunctionsFileManager(RealmConfigurationBase config, string function, HttpMessageHandler? httpHandler = null)
        : base(config)
    {
        _function = function;

        _user = config switch
        {
            SyncConfigurationBase syncConfig => syncConfig.User,
            _ => throw new NotSupportedException("This manager only supports synchronized Realms")
        };

        _client = httpHandler == null ? new HttpClient() : new HttpClient(httpHandler);
    }

    /// <inheritdoc/>
    protected override async Task DeleteFileCore(string id)
    {
        await CallSignFunction<DeleteResponse>(id, OperationType.Delete);
    }

    /// <inheritdoc/>
    protected override async Task DownloadFileCore(string id, string file)
    {
        var response = await CallSignFunction<DownloadResponse>(id, OperationType.Download);
        var url = response.Url;

        var stream = await _client.GetStreamAsync(new Uri(url));
        var fileStream = new FileStream(file, FileMode.Create);
        await stream.CopyToAsync(fileStream);
    }

    /// <inheritdoc/>
    protected override async Task<string> UploadFileCore(string id, string file)
    {
        var response = await CallSignFunction<UploadResponse>(id, OperationType.Upload);
        var fileStream = new FileStream(file, FileMode.Open);
        var streamContent = new StreamContent(fileStream);
        
        // TODO: this doesn't do multipart uploads. See
        // https://stackoverflow.com/questions/29974416/how-do-i-upload-to-amazon-s3-using-net-httpclient-without-using-their-sdk
        // for example how to do it.
        await _client.PutAsync(new Uri(response.PresignedUrl), streamContent);

        return response.CanonicalUrl;
    }

    private async Task<T> CallSignFunction<T>(string id, OperationType operation)
        where T : ResponseBase
    {
        var payload = new FunctionPayload(id, OperationType.Upload);
        var response = await _user.Functions.CallAsync<T>(_function, payload);

        if (!response.Success)
        {
            throw new Exception($"Failed to {operation} object with Id: {id}: {response.Error}");
        }

        return response;
    }

    private class FunctionPayload
    {
        public string FileId { get; set;  }
        
        [BsonRepresentation(BsonType.String)]
        public OperationType Operation { get; set; }

        public FunctionPayload(string id, OperationType operation)
        {
            FileId = id;
            Operation = operation;
        }
    }

    private abstract class ResponseBase
    {
        [MemberNotNullWhen(false, nameof(Error))]
        public bool Success { get; set; }
        
        public string? Error { get; set; }
    }
    
    private class UploadResponse : ResponseBase
    {
        public required string PresignedUrl { get; set; }
        
        public required string CanonicalUrl { get; set; }
    }
    
    private class DownloadResponse : ResponseBase
    {
        public required string Url { get; set; }
    }

    private class DeleteResponse : ResponseBase
    {
    }

    private enum OperationType
    {
        Upload,
        Download,
        Delete
    }
}

