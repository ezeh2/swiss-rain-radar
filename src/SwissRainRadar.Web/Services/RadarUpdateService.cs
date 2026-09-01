using System.Text;
using System.Text.Json;
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

        var latest = assets[^1];
        var selected = SelectNonOverlappingHours(assets, latest.Timestamp, _options.PeriodsHours.Max());
        var grids = new List<RadarGrid>(selected.Count);

        foreach (var asset in selected)
        {
            await EnsureRawAssetAsync(asset, cancellationToken);
            await using var stream = await objectStore.OpenReadAsync(RawContainer, RawPath(asset), cancellationToken)
                ?? throw new InvalidOperationException($"Raw radar asset {asset.Name} is unavailable after import.");
            grids.Add(reader.Read(stream));
        }

        var variants = new List<MapVariant>();
        foreach (var period in _options.PeriodsHours.Order())
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
            _options.PeriodsHours.Max());
        var json = JsonSerializer.Serialize(manifest, JsonOptions);
        await using var manifestStream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        await objectStore.PutAsync(MapsContainer, "latest.json", manifestStream, "application/json", cancellationToken);

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
