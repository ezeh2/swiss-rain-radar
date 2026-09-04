using SwissRainRadar.Web.Models;

namespace SwissRainRadar.Web.Services;

/// <summary>
/// Creates rendered rain-map files from stored radar measurements.
/// Input files: one or more hourly HDF5 radar files from the <c>raw</c> container.
/// Output files: PNG rain maps in the <c>maps/history</c> hierarchy. The processor reads the
/// source grids, calculates each requested accumulation period, renders it, and writes one PNG
/// for every period for which enough source data is available.
/// </summary>
public sealed partial class RainMapFileProcessor(
    IObjectStore objectStore,
    HdfRadarReader reader,
    RainfallAggregator aggregator,
    RadarImageRenderer renderer,
    ILogger<RainMapFileProcessor> logger)
{
    private const string RawContainer = "raw";
    private const string MapsContainer = "maps";

    public async Task<IReadOnlyList<MapVariant>> CreateRainMapFilesAsync(
        IReadOnlyList<RadarAsset> sourceFiles,
        DateTimeOffset periodEnd,
        IReadOnlyList<int> periodsHours,
        CancellationToken cancellationToken)
    {
        var grids = new List<RadarGrid>(sourceFiles.Count);
        foreach (var sourceFile in sourceFiles)
        {
            await using var stream = await objectStore.OpenReadAsync(
                    RawContainer,
                    RawRadarFile.Path(sourceFile),
                    cancellationToken)
                ?? throw new InvalidOperationException($"Raw radar asset {sourceFile.Name} is unavailable.");
            grids.Add(reader.Read(stream));
        }

        var variants = new List<MapVariant>();
        foreach (var period in periodsHours)
        {
            if (grids.Count < period)
            {
                LogIncompletePeriod(grids.Count, period);
                continue;
            }

            var grid = aggregator.Sum(grids.Take(period).ToArray());
            await using var png = await renderer.RenderAsync(grid, cancellationToken);
            await objectStore.PutAsync(
                MapsContainer,
                MapFile.Path(periodEnd, period),
                png,
                "image/png",
                cancellationToken);
            variants.Add(MapFile.Variant(periodEnd, period));
        }

        return variants;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Only {count} of {period} raw hourly grids are available.")]
    private partial void LogIncompletePeriod(int count, int period);
}
