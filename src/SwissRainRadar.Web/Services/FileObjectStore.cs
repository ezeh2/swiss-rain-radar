using System.Text;
using Microsoft.Extensions.Options;
using SwissRainRadar.Web.Options;

namespace SwissRainRadar.Web.Services;

public sealed class FileObjectStore(IWebHostEnvironment environment, IOptions<StorageOptions> options) : IObjectStore
{
    private readonly string _root = Path.GetFullPath(
        Path.Combine(environment.ContentRootPath, options.Value.LocalRoot));

    public Task<bool> ExistsAsync(string container, string path, CancellationToken cancellationToken)
    {
        return Task.FromResult(File.Exists(GetPath(container, path)));
    }

    public async Task PutAsync(
        string container,
        string path,
        Stream content,
        string contentType,
        CancellationToken cancellationToken)
    {
        var filePath = GetPath(container, path);
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

        var temporaryPath = filePath + ".tmp";
        await using (var output = File.Create(temporaryPath))
        {
            await content.CopyToAsync(output, cancellationToken);
        }

        File.Move(temporaryPath, filePath, overwrite: true);
    }

    public Task<Stream?> OpenReadAsync(string container, string path, CancellationToken cancellationToken)
    {
        Stream? stream = File.Exists(GetPath(container, path))
            ? File.OpenRead(GetPath(container, path))
            : null;
        return Task.FromResult(stream);
    }

    public async Task<string?> ReadTextAsync(string container, string path, CancellationToken cancellationToken)
    {
        var filePath = GetPath(container, path);
        return File.Exists(filePath)
            ? await File.ReadAllTextAsync(filePath, Encoding.UTF8, cancellationToken)
            : null;
    }

    private string GetPath(string container, string path)
    {
        var combined = Path.GetFullPath(Path.Combine(_root, container, path.Replace('/', Path.DirectorySeparatorChar)));
        var allowedRoot = Path.GetFullPath(Path.Combine(_root, container)) + Path.DirectorySeparatorChar;

        if (!combined.StartsWith(allowedRoot, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The requested object path escapes its container.");
        }

        return combined;
    }
}

