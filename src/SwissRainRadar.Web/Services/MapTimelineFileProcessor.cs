using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SwissRainRadar.Web.Models;
using SwissRainRadar.Web.Options;

namespace SwissRainRadar.Web.Services;

/// <summary>
/// Builds the retained map timeline from the available historical rain maps.
/// Input files: PNG rain maps from the <c>maps/history</c> hierarchy. Output file:
/// <c>maps/timeline.json</c>. The processor filters unsupported and expired maps, groups the
/// remaining variants by timestamp, orders them, and rebuilds the complete timeline document.
/// </summary>
public sealed class MapTimelineFileProcessor(IObjectStore objectStore, IOptions<RadarOptions> options)
{
    private const string MapsContainer = "maps";

    public async Task CreateTimelineAsync(DateTimeOffset cutoff, CancellationToken cancellationToken)
    {
        var paths = await objectStore.ListAsync(MapsContainer, "history/", cancellationToken);
        var timeline = BuildTimelineFromPaths(paths, options.Value.PeriodsHours, cutoff);
        var json = JsonSerializer.Serialize(timeline, MapFile.JsonOptions);
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        await objectStore.PutAsync(MapsContainer, "timeline.json", stream, "application/json", cancellationToken);
    }

    public static MapTimeline BuildTimelineFromPaths(
        IEnumerable<string> paths,
        IEnumerable<int> supportedPeriods,
        DateTimeOffset cutoff)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(supportedPeriods);
        var supported = supportedPeriods.ToHashSet();
        var snapshots = paths
            .Select(MapFile.TryParse)
            .Where(result => result is not null
                && result.Value.PeriodEnd >= cutoff
                && supported.Contains(result.Value.Hours))
            .Select(result => result!.Value)
            .GroupBy(result => result.PeriodEnd)
            .OrderBy(group => group.Key)
            .Select(group => new MapSnapshot(
                group.Key,
                group.Select(result => MapFile.Variant(result.PeriodEnd, result.Hours))
                    .DistinctBy(variant => variant.Hours)
                    .OrderBy(variant => variant.Hours)
                    .ToArray()))
            .ToArray();
        return new MapTimeline(snapshots);
    }
}
