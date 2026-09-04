using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using SwissRainRadar.Web.Models;
using SwissRainRadar.Web.Options;

namespace SwissRainRadar.Web.Services;

/// <summary>
/// A client for the MeteoSwiss radar data service. It queries the STAC API for available assets and downloads them as needed.
/// </summary>
/// <param name="httpClient"></param>
/// <param name="options"></param> 
public sealed partial class MeteoSwissClient(HttpClient httpClient, IOptions<RadarOptions> options)
{
    private readonly RadarOptions _options = options.Value;
    private readonly JsonSerializerOptions _jsonSerializerOptions = new JsonSerializerOptions { WriteIndented = true };

    public async Task<IReadOnlyList<RadarAsset>> GetAssetsAsync(
        DateOnly date,
        CancellationToken cancellationToken)
    {
        var baseUriString = _options.StacBaseUrl.OriginalString.TrimEnd('/') + '/';
        var itemUrl = new Uri(baseUriString + $"{date:yyyyMMdd}-ch");
        using var response = await httpClient.GetAsync(itemUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return Array.Empty<RadarAsset>();
        }

        response.EnsureSuccessStatusCode();
        await using var content = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(content, cancellationToken: cancellationToken);

        // write document to file for debugging, filename from itemUrl, e.g. "20230901-ch.json"
        var fileName = itemUrl.Segments[^1];
        using var fileStream = new FileStream(fileName, FileMode.Create, FileAccess.Write);
        await JsonSerializer.SerializeAsync(fileStream, document, _jsonSerializerOptions, cancellationToken);   

        if (!document.RootElement.TryGetProperty("assets", out var assetsElement))
        {
            return Array.Empty<RadarAsset>();
        }

        // loop over assets in the JSON document
        // one assert for every 5 minutes, i.e. for 24h there are 24 * 60 / 5 = 288 assets
        var assets = new List<RadarAsset>();
        foreach (var assetProperty in assetsElement.EnumerateObject())
        {
            if (!TryParseTimestamp(assetProperty.Name, out var timestamp)
                || !assetProperty.Value.TryGetProperty("href", out var hrefElement)
                || !Uri.TryCreate(hrefElement.GetString(), UriKind.Absolute, out var href))
            {
                continue;
            }

            assets.Add(new RadarAsset(assetProperty.Name, href, timestamp));
        }

        return assets.OrderBy(asset => asset.Timestamp).ToArray();
    }

    public async Task<Stream> DownloadAsync(RadarAsset asset, CancellationToken cancellationToken)
    {
        var response = await httpClient.GetAsync(
            asset.DownloadUri,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStreamAsync(cancellationToken);
    }

    public static bool TryParseTimestamp(string fileName, out DateTimeOffset timestamp)
    {
        var match = CpcFileNameRegex().Match(fileName);
        if (!match.Success)
        {
            timestamp = default;
            return false;
        }

        var year = 2000 + int.Parse(match.Groups["year"].Value, CultureInfo.InvariantCulture);
        var day = int.Parse(match.Groups["day"].Value, CultureInfo.InvariantCulture);
        var hour = int.Parse(match.Groups["hour"].Value, CultureInfo.InvariantCulture);
        var minute = int.Parse(match.Groups["minute"].Value, CultureInfo.InvariantCulture);

        timestamp = new DateTimeOffset(year, 1, 1, hour, minute, 0, TimeSpan.Zero).AddDays(day - 1);
        return true;
    }

    [GeneratedRegex(
        "^cpc(?<year>\\d{2})(?<day>\\d{3})(?<hour>\\d{2})(?<minute>\\d{2})\\d_00060\\..+\\.h5$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CpcFileNameRegex();
}

