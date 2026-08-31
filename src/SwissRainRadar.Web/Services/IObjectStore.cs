namespace SwissRainRadar.Web.Services;

public interface IObjectStore
{
    Task<bool> ExistsAsync(string container, string path, CancellationToken cancellationToken);

    Task PutAsync(
        string container,
        string path,
        Stream content,
        string contentType,
        CancellationToken cancellationToken);

    Task<Stream?> OpenReadAsync(string container, string path, CancellationToken cancellationToken);

    Task<string?> ReadTextAsync(string container, string path, CancellationToken cancellationToken);
}

