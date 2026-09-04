using SwissRainRadar.Web.Models;

namespace SwissRainRadar.Web.Services;

/// <summary>
/// Imports raw radar files from MeteoSwiss into the configured object store.
/// Input: RadarAsset-objects parsed from MeteoSwiss asset metadata. 
/// Output: stored HDF5 radar files under the <c>raw</c> container. under the <c>raw</c> container. 
/// example file name: <c>raw/2026/08/31/cpc2624300009_00060.001.h5</c>
/// 
/// Existing files are skipped, so the operation can safely
/// process a batch containing files that were imported previously.
/// </summary>
public sealed class RawRadarFileImporter(MeteoSwissClient meteoSwissClient, IObjectStore objectStore)
{
    private const string RawContainer = "raw";

    public async Task DownloadRawRadarFilesAsync(
        IReadOnlyList<RadarAsset> assets,
        CancellationToken cancellationToken)
    {
        foreach (var asset in assets)
        {
            var path = RawRadarFile.Path(asset);
            if (await objectStore.ExistsAsync(RawContainer, path, cancellationToken))
            {
                continue;
            }

            await using var download = await meteoSwissClient.DownloadAsync(asset, cancellationToken);
            await objectStore.PutAsync(RawContainer, path, download, "application/x-hdf5", cancellationToken);
        }
    }
}

/// <summary>
/// Defines the storage path of a raw HDF5 radar file. It does not read or write files.
/// Input: radar asset metadata. Output: the corresponding HDF5 object-store path.
/// </summary>
internal static class RawRadarFile
{
    public static string Path(RadarAsset asset) => $"{asset.Timestamp:yyyy/MM/dd}/{asset.Name}";
}
