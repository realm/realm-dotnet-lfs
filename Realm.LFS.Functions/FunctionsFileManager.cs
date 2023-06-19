using System.Diagnostics.CodeAnalysis;
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
    private readonly bool _usePublicDownloads;
    private readonly HttpClient _client;
    private readonly User _user;
    
    public FunctionsFileManager(RealmConfigurationBase config, string function, bool usePublicDownloads = true, HttpMessageHandler? httpHandler = null)
        : base(config)
    {
        _function = function;
        _usePublicDownloads = usePublicDownloads;

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
        var payload = new FunctionPayload
        {
            FileId = id,
            Operation = OperationType.Delete
        };

        var response = await _user.Functions.CallAsync<DeleteResponse>(_function, payload);

        if (!response.Success)
        {
            throw new Exception($"Failed to delete object with Id: {id}: {response.Error}");
        }
    }

    /// <inheritdoc/>
    protected override async Task DownloadFileCore(string id, string file, FileData fileData)
    {
        string url;
        if (_usePublicDownloads)
        {
            url = fileData.Url ?? throw new ArgumentException("This method should only be invoked with remote fileData",
                nameof(fileData));
        }
        else
        {
            var response = await GetPresignedUrl(id, OperationType.Download);
            url = response.PresignedUrl;
        }

        var stream = await _client.GetStreamAsync(new Uri(url));
        var fileStream = new FileStream(file, FileMode.Create);
        await stream.CopyToAsync(fileStream);
    }

    /// <inheritdoc/>
    protected override async Task<string> UploadFileCore(string id, string file)
    {
        var response = await GetPresignedUrl(id, OperationType.Upload);
        var fileStream = new FileStream(file, FileMode.Open);
        var streamContent = new StreamContent(fileStream);
        await _client.PutAsync(new Uri(response.PresignedUrl), streamContent);

        return response.CanonicalUrl!;
    }

    private async Task<SignedUrlResponse> GetPresignedUrl(string id, OperationType operation)
    {
        var payload = new FunctionPayload
        {
            FileId = id,
            Operation = operation
        };

        return await _user.Functions.CallAsync<SignedUrlResponse>(_function, payload);
    }

    private class FunctionPayload
    {
        public required string FileId { get; init; }
        
        public required OperationType Operation { get; init; }
    }

    private class SignedUrlResponse
    {
        public required string PresignedUrl { get; set; }
        
        public string? CanonicalUrl { get; set; }
    }

    private class DeleteResponse
    {
        [MemberNotNullWhen(false, nameof(Error))]
        public bool Success { get; set; }
        
        public string? Error { get; set; }
    }

    private enum OperationType
    {
        Upload,
        Download,
        Delete
    }
}

