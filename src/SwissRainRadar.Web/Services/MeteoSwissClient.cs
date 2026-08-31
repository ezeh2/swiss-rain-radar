using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using SwissRainRadar.Web.Models;
using SwissRainRadar.Web.Options;

namespace SwissRainRadar.Web.Services;

public sealed partial class MeteoSwissClient(HttpClient httpClient, IOptions<RadarOptions> options)
{
    private readonly RadarOptions _options = options.Value;

    public async Task<IReadOnlyList<RadarAsset>> GetAssetsAsync(
        DateOnly date,
        CancellationToken cancellationToken)
    {
        var itemUrl = new Uri(_options.StacBaseUrl, $"{date:yyyyMMdd}-ch");
        using var response = await httpClient.GetAsync(itemUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return [];
        }

        response.EnsureSuccessStatusCode();
        await using var content = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(content, cancellationToken: cancellationToken);

        if (!document.RootElement.TryGetProperty("assets", out var assetsElement))
        {
            return [];
        }

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

