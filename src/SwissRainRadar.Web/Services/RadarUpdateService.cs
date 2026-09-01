using System.Text;
using System.Text.Json;
using System.Globalization;
using Microsoft.Extensions.Options;
using SwissRainRadar.Web.Models;
using SwissRainRadar.Web.Options;

namespace SwissRainRadar.Web.Services;

public sealed partial class RadarUpdateService(
    MeteoSwissClient meteoSwissClient,
    IObjectStore objectStore,
    HdfRadarReader reader,
    RainfallAggregator aggregator,
    RadarImageRenderer renderer,
    TimeProvider timeProvider,
    IOptions<RadarOptions> options,
    ILogger<RadarUpdateService> logger)
{
    private const string RawContainer = "raw";
    private const string MapsContainer = "maps";
    private static readonly MapBounds Bounds = new(43.619, 2.68942, 49.3744, 12.4623);
    private readonly RadarOptions _options = options.Value;

    public async Task UpdateLatestAsync(CancellationToken cancellationToken)
    {
        var referenceTime = timeProvider.GetUtcNow();
        var assets = await GetRecentAssetsAsync(days: 2, referenceTime, cancellationToken);
        if (assets.Count == 0)
        {
            LogNoAssets();
            return;
        }

        var periods = NormalizePeriods(_options.PeriodsHours);
        var latest = assets[^1];
        var selected = SelectNonOverlappingHours(assets, latest.Timestamp, periods[^1]);
        var grids = new List<RadarGrid>(selected.Count);

        foreach (var asset in selected)
        {
            await EnsureRawAssetAsync(asset, cancellationToken);
            await using var stream = await objectStore.OpenReadAsync(RawContainer, RawPath(asset), cancellationToken)
                ?? throw new InvalidOperationException($"Raw radar asset {asset.Name} is unavailable after import.");
            grids.Add(reader.Read(stream));
        }

        var variants = new List<MapVariant>();
        foreach (var period in periods)
        {
            if (grids.Count < period)
            {
                LogIncompletePeriod(grids.Count, period);
                continue;
            }

            var grid = aggregator.Sum(grids.Take(period).ToArray());
            await using var png = await renderer.RenderAsync(grid, cancellationToken);
            var mapPath = $"history/{latest.Timestamp:yyyyMMddHHmm}/{period}h.png";
            await objectStore.PutAsync(MapsContainer, mapPath, png, "image/png", cancellationToken);
            variants.Add(new MapVariant(period, $"/api/maps/{latest.Timestamp:yyyyMMddHHmm}/{period}"));
        }

        var manifest = new MapManifest(
            referenceTime,
            latest.Timestamp,
            variants,
            Bounds,
            "MeteoSwiss CombiPrecip",
            grids.Count,
            periods[^1]);
        var json = JsonSerializer.Serialize(manifest, JsonOptions);
        await using var manifestStream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        await objectStore.PutAsync(MapsContainer, "latest.json", manifestStream, "application/json", cancellationToken);

        if (variants.Count > 0)
        {
            await UpdateTimelineAsync(
                new MapSnapshot(latest.Timestamp, variants),
                referenceTime.AddDays(-_options.TimelineRetentionDays),
                cancellationToken);
        }

        LogPublished(variants.Count, latest.Timestamp);
    }

    public async Task BackfillRawAsync(CancellationToken cancellationToken)
    {
        if (!_options.BackfillOnStartup)
        {
            return;
        }

        var referenceTime = timeProvider.GetUtcNow();
        var assets = await GetRecentAssetsAsync(_options.RawRetentionDays, referenceTime, cancellationToken);
        LogBackfill(assets.Count);

        foreach (var asset in assets)
        {
            await EnsureRawAssetAsync(asset, cancellationToken);
        }
    }

    public static IReadOnlyList<RadarAsset> SelectNonOverlappingHours(
        IReadOnlyList<RadarAsset> assets,
        DateTimeOffset periodEnd,
        int hours)
    {
        var result = new List<RadarAsset>(hours);
        for (var offset = 0; offset < hours; offset++)
        {
            var target = periodEnd.AddHours(-offset);
            var candidate = assets
                .Where(asset => asset.Timestamp <= target && target - asset.Timestamp <= TimeSpan.FromMinutes(7))
                .MaxBy(asset => asset.Timestamp);

            if (candidate is null)
            {
                break;
            }

            result.Add(candidate);
        }

        return result;
    }

    public static IReadOnlyList<RadarAsset> SelectAssetsAtOrBefore(
        IEnumerable<RadarAsset> assets,
        DateTimeOffset referenceTime)
    {
        ArgumentNullException.ThrowIfNull(assets);

        return assets
            .Where(asset => asset.Timestamp <= referenceTime)
            .OrderBy(asset => asset.Timestamp)
            .ToArray();
    }

    public static IReadOnlyList<int> NormalizePeriods(IEnumerable<int> periods)
    {
        ArgumentNullException.ThrowIfNull(periods);

        return periods
            .Distinct()
            .Order()
            .ToArray();
    }

    public static MapTimeline MergeTimeline(
        MapTimeline? timeline,
        MapSnapshot snapshot,
        DateTimeOffset cutoff)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var snapshots = (timeline?.Snapshots ?? [])
            .Where(item => item.PeriodEnd >= cutoff && item.PeriodEnd != snapshot.PeriodEnd)
            .Append(snapshot)
            .OrderBy(item => item.PeriodEnd)
            .ToArray();

        return new MapTimeline(snapshots);
    }

    public static MapTimeline BuildTimelineFromPaths(
        IEnumerable<string> paths,
        IEnumerable<int> supportedPeriods,
        DateTimeOffset cutoff)
    {
        ArgumentNullException.ThrowIfNull(paths);
        var supported = supportedPeriods.ToHashSet();
        var variantsByTime = new Dictionary<DateTimeOffset, Dictionary<int, MapVariant>>();

        foreach (var path in paths)
        {
            var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 3
                || parts[0] != "history"
                || !DateTimeOffset.TryParseExact(
                    parts[1],
                    "yyyyMMddHHmm",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var periodEnd)
                || periodEnd < cutoff
                || !parts[2].EndsWith("h.png", StringComparison.Ordinal)
                || !int.TryParse(parts[2][..^5], CultureInfo.InvariantCulture, out var hours)
                || !supported.Contains(hours))
            {
                continue;
            }

            if (!variantsByTime.TryGetValue(periodEnd, out var variants))
            {
                variants = new Dictionary<int, MapVariant>();
                variantsByTime.Add(periodEnd, variants);
            }

            variants[hours] = new MapVariant(hours, $"/api/maps/{periodEnd:yyyyMMddHHmm}/{hours}");
        }

        var snapshots = variantsByTime
            .OrderBy(item => item.Key)
            .Select(item => new MapSnapshot(
                item.Key,
                item.Value.Values.OrderBy(variant => variant.Hours).ToArray()))
            .ToArray();
        return new MapTimeline(snapshots);
    }

    private async Task<IReadOnlyList<RadarAsset>> GetRecentAssetsAsync(
        int days,
        DateTimeOffset referenceTime,
        CancellationToken cancellationToken)
    {
        var result = new List<RadarAsset>();
        var today = DateOnly.FromDateTime(referenceTime.UtcDateTime);
        for (var offset = days - 1; offset >= 0; offset--)
        {
            result.AddRange(await meteoSwissClient.GetAssetsAsync(today.AddDays(-offset), cancellationToken));
        }

        return SelectAssetsAtOrBefore(result, referenceTime);
    }

    private async Task EnsureRawAssetAsync(RadarAsset asset, CancellationToken cancellationToken)
    {
        var path = RawPath(asset);
        if (await objectStore.ExistsAsync(RawContainer, path, cancellationToken))
        {
            return;
        }

        await using var download = await meteoSwissClient.DownloadAsync(asset, cancellationToken);
        await objectStore.PutAsync(RawContainer, path, download, "application/x-hdf5", cancellationToken);
    }

    private async Task UpdateTimelineAsync(
        MapSnapshot snapshot,
        DateTimeOffset cutoff,
        CancellationToken cancellationToken)
    {
        var existingJson = await objectStore.ReadTextAsync(MapsContainer, "timeline.json", cancellationToken);
        var existing = existingJson is null
            ? BuildTimelineFromPaths(
                await objectStore.ListAsync(MapsContainer, "history/", cancellationToken),
                _options.PeriodsHours,
                cutoff)
            : JsonSerializer.Deserialize<MapTimeline>(existingJson, JsonOptions);
        var timeline = MergeTimeline(existing, snapshot, cutoff);
        var json = JsonSerializer.Serialize(timeline, JsonOptions);
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        await objectStore.PutAsync(MapsContainer, "timeline.json", stream, "application/json", cancellationToken);
    }

    private static string RawPath(RadarAsset asset) => $"{asset.Timestamp:yyyy/MM/dd}/{asset.Name}";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    [LoggerMessage(Level = LogLevel.Warning, Message = "No MeteoSwiss CPC assets are currently available.")]
    private partial void LogNoAssets();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Only {count} of {period} raw hourly grids are available.")]
    private partial void LogIncompletePeriod(int count, int period);

    [LoggerMessage(Level = LogLevel.Information, Message = "Published {count} rain maps ending at {timestamp}.")]
    private partial void LogPublished(int count, DateTimeOffset timestamp);

    [LoggerMessage(Level = LogLevel.Information, Message = "Checking {count} raw CPC files for the retention window.")]
    private partial void LogBackfill(int count);
}
