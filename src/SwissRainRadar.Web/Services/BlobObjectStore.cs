using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;
using SwissRainRadar.Web.Options;

namespace SwissRainRadar.Web.Services;

public sealed class BlobObjectStore : IObjectStore
{
    private readonly BlobServiceClient _client;

    public BlobObjectStore(IOptions<StorageOptions> options)
    {
        var accountUri = options.Value.AccountUri
            ?? throw new InvalidOperationException("Storage:AccountUri is required for Azure Blob Storage.");
        _client = new BlobServiceClient(new Uri(accountUri), new DefaultAzureCredential());
    }

    public async Task<bool> ExistsAsync(string container, string path, CancellationToken cancellationToken)
    {
        return (await GetBlob(container, path).ExistsAsync(cancellationToken)).Value;
    }

    public async Task PutAsync(
        string container,
        string path,
        Stream content,
        string contentType,
        CancellationToken cancellationToken)
    {
        var options = new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = contentType }
        };
        await GetBlob(container, path).UploadAsync(content, options, cancellationToken);
    }

    public async Task<Stream?> OpenReadAsync(string container, string path, CancellationToken cancellationToken)
    {
        var blob = GetBlob(container, path);
        if (!(await blob.ExistsAsync(cancellationToken)).Value)
        {
            return null;
        }

        return await blob.OpenReadAsync(cancellationToken: cancellationToken);
    }

    public async Task<string?> ReadTextAsync(string container, string path, CancellationToken cancellationToken)
    {
        var blob = GetBlob(container, path);
        if (!(await blob.ExistsAsync(cancellationToken)).Value)
        {
            return null;
        }

        var response = await blob.DownloadContentAsync(cancellationToken);
        return response.Value.Content.ToString();
    }

    private BlobClient GetBlob(string container, string path)
    {
        return _client.GetBlobContainerClient(container).GetBlobClient(path);
    }
}
