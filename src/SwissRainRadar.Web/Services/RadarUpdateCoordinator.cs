using Microsoft.Extensions.Options;
using SwissRainRadar.Web.Models;
using SwissRainRadar.Web.Options;

namespace SwissRainRadar.Web.Services;

/// <summary>
/// Coordinates the radar update pipeline without processing files itself.
/// Input: MeteoSwiss asset metadata and the configured update schedule. Output: none directly;
/// it delegates HDF5-to-HDF5, HDF5-to-PNG, and PNG-to-JSON work to the specialized services.
/// It selects the relevant time window, invokes each processing stage in order, and coordinates
/// both the latest-map update and the optional raw-file backfill.
/// </summary>
public sealed partial class RadarUpdateCoordinator(
    MeteoSwissClient meteoSwissClient,
    RawRadarFileImporter rawFileImporter,
    RainMapFileProcessor rainMapFileProcessor,
    MapManifestFileProcessor manifestFileProcessor,
    MapTimelineFileProcessor timelineFileProcessor,
    TimeProvider timeProvider,
    IOptions<RadarOptions> options,
    ILogger<RadarUpdateCoordinator> logger)
{
    private const int LatestAssetQueryDays = 2;
    private readonly RadarOptions _options = options.Value;

    public async Task UpdateLatestAsync(CancellationToken cancellationToken)
    {
        var referenceTime = timeProvider.GetUtcNow();
        var assets = await GetRecentAssetsAsync(LatestAssetQueryDays, referenceTime, cancellationToken);
        if (assets.Count == 0)
        {
            LogNoAssets();
            return;
        }

        var periods = NormalizePeriods(_options.PeriodsHours);
        var latest = assets[^1];
        var selected = SelectNonOverlappingHours(assets, latest.Timestamp, periods[^1]);

        await rawFileImporter.DownloadRawRadarFilesAsync(selected, cancellationToken);
        var variants = await rainMapFileProcessor.CreateRainMapFilesAsync(
            selected,
            latest.Timestamp,
            periods,
            cancellationToken);

        await manifestFileProcessor.CreateLatestManifestAsync(
            latest.Timestamp,
            referenceTime,
            selected.Count,
            periods,
            cancellationToken);

        if (variants.Count > 0)
        {
            await timelineFileProcessor.CreateTimelineAsync(
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
        await rawFileImporter.DownloadRawRadarFilesAsync(assets, cancellationToken);
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
        return periods.Distinct().Order().ToArray();
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

    [LoggerMessage(Level = LogLevel.Warning, Message = "No MeteoSwiss CPC assets are currently available.")]
    private partial void LogNoAssets();

    [LoggerMessage(Level = LogLevel.Information, Message = "Published {count} rain maps ending at {timestamp}.")]
    private partial void LogPublished(int count, DateTimeOffset timestamp);

    [LoggerMessage(Level = LogLevel.Information, Message = "Checking {count} raw CPC files for the retention window.")]
    private partial void LogBackfill(int count);
}
