using System.Text;
using System.Text.Json;
using SwissRainRadar.Web.Models;

namespace SwissRainRadar.Web.Services;

/// <summary>
/// Publishes the manifest describing the latest available rain maps.
/// Input files: PNG rain maps for one timestamp from the <c>maps/history</c> hierarchy.
/// Output file: <c>maps/latest.json</c>. The processor discovers supported PNG variants and
/// records their URLs, map bounds, source information, update time, and data completeness.
/// </summary>
public sealed class MapManifestFileProcessor(IObjectStore objectStore)
{
    private const string MapsContainer = "maps";
    private static readonly MapBounds Bounds = new(43.619, 2.68942, 49.3744, 12.4623);

    public async Task CreateLatestManifestAsync(
        DateTimeOffset periodEnd,
        DateTimeOffset updatedAt,
        int availableRawHours,
        IReadOnlyList<int> supportedPeriodsHours,
        CancellationToken cancellationToken)
    {
        var supportedPeriods = supportedPeriodsHours.ToHashSet();
        var paths = await objectStore.ListAsync(MapsContainer, MapFile.TimestampPrefix(periodEnd), cancellationToken);
        var variants = paths
            .Select(MapFile.TryParse)
            .Where(result => result is not null
                && result.Value.PeriodEnd == periodEnd
                && supportedPeriods.Contains(result.Value.Hours))
            .Select(result => MapFile.Variant(result!.Value.PeriodEnd, result.Value.Hours))
            .OrderBy(variant => variant.Hours)
            .ToArray();

        var manifest = new MapManifest(
            updatedAt,
            periodEnd,
            variants,
            Bounds,
            "MeteoSwiss CombiPrecip",
            availableRawHours,
            supportedPeriodsHours[^1]);
        await WriteJsonAsync("latest.json", manifest, cancellationToken);
    }

    private async Task WriteJsonAsync<T>(string path, T value, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(value, MapFile.JsonOptions);
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        await objectStore.PutAsync(MapsContainer, path, stream, "application/json", cancellationToken);
    }
}
