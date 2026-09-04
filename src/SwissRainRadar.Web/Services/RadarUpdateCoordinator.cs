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
    // CPC assets normally arrive every five minutes. Allow two additional minutes for
    // delayed or missing publications while keeping adjacent hourly windows separate.
    private static readonly TimeSpan MaximumAssetDelay = TimeSpan.FromMinutes(7);
    private readonly RadarOptions _options = options.Value;

    public async Task UpdateLatestAsync(CancellationToken cancellationToken)
    {
        // if FixedReferenceTimeUtc in appsettings.json is set, then timeProvider is object of class FixedTimeProvider, 
        // otherwise it is object of class SystemTimeProvider
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

    /// <summary>
    /// eliminates overlapping assets and selects the most recent asset for each hour in the given period, starting from the periodEnd and going backwards
    /// </summary>
    /// <param name="assets"></param>
    /// <param name="periodEnd"></param>
    /// <param name="hours"></param>
    /// <returns></returns>
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
                .Where(asset => asset.Timestamp <= target && target - asset.Timestamp <= MaximumAssetDelay)
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

    /// <summary>
    /// eliminates duplicates and sort the periods in ascending order, e.g. [1, 3, 2, 1] -> [1, 2, 3]
    /// </summary>
    /// <param name="periods"></param>
    /// <returns></returns>
    public static IReadOnlyList<int> NormalizePeriods(IEnumerable<int> periods)
    {
        ArgumentNullException.ThrowIfNull(periods);
        return periods.Distinct().Order().ToArray();
    }

    /// <summary>
    /// * downloads json-files for the last <paramref name="days"/> days from MeteoSwiss STAC API
    /// * extracts the radar assets from the json-files
    /// * selects the assets that are at or before the <paramref name="referenceTime"/>
    /// * returns the selected assets ordered by timestamp ascending
    /// </summary>
    /// <param name="days"></param>
    /// <param name="referenceTime"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    private async Task<IReadOnlyList<RadarAsset>> GetRecentAssetsAsync(
        int days,
        DateTimeOffset referenceTime,
        CancellationToken cancellationToken)
    {
        var result1 = new List<RadarAsset>();
        var today = DateOnly.FromDateTime(referenceTime.UtcDateTime);
        // if days==2 then offset = 1,0; i.e. 48 hours of data
        // if days==3 then offset = 2,1,0; i.e. 72 hours of data
        for (var offset = days - 1; offset >= 0; offset--)
        {
            // for every 5 minutes 1 asset, i.e. 24 * 60 / 5 = 288 assets per day
            result1.AddRange(await meteoSwissClient.GetAssetsAsync(today.AddDays(-offset), cancellationToken));
        }

        // expected number of assets for the given days, e.g. 2 days = 288 * 2 = 576 assets
        int cnt1 = result1.Count;

        var result2 = SelectAssetsAtOrBefore(result1, referenceTime);
        
        // expected number of assets for the given days, e.g. 2 days = 288 * 2 = 576 assets
        int cnt2 = result2.Count;
        return result2;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "No MeteoSwiss CPC assets are currently available.")]
    private partial void LogNoAssets();

    [LoggerMessage(Level = LogLevel.Information, Message = "Published {count} rain maps ending at {timestamp}.")]
    private partial void LogPublished(int count, DateTimeOffset timestamp);

    [LoggerMessage(Level = LogLevel.Information, Message = "Checking {count} raw CPC files for the retention window.")]
    private partial void LogBackfill(int count);
}
