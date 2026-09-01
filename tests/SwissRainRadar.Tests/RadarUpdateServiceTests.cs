using SwissRainRadar.Web.Models;
using SwissRainRadar.Web.Services;

namespace SwissRainRadar.Tests;

public sealed class RadarUpdateServiceTests
{
    [Fact]
    public void SelectNonOverlappingHours_SelectsOneFilePerHour()
    {
        var end = new DateTimeOffset(2026, 8, 31, 8, 30, 0, TimeSpan.Zero);
        var assets = Enumerable.Range(0, 37)
            .Select(index => end.AddMinutes(-5 * index))
            .Select(time => new RadarAsset($"file-{time:HHmm}", new Uri("https://example.test/file"), time))
            .OrderBy(asset => asset.Timestamp)
            .ToArray();

        var selected = RadarUpdateService.SelectNonOverlappingHours(assets, end, 4);

        Assert.Equal(4, selected.Count);
        Assert.Equal([end, end.AddHours(-1), end.AddHours(-2), end.AddHours(-3)],
            selected.Select(asset => asset.Timestamp));
    }

    [Fact]
    public void SelectNonOverlappingHours_StopsAtFirstDataGap()
    {
        var end = new DateTimeOffset(2026, 8, 31, 8, 30, 0, TimeSpan.Zero);
        RadarAsset[] assets =
        [
            new("latest", new Uri("https://example.test/latest"), end),
            new("old", new Uri("https://example.test/old"), end.AddHours(-2))
        ];

        var selected = RadarUpdateService.SelectNonOverlappingHours(assets, end, 3);

        Assert.Single(selected);
    }

    [Fact]
    public void SelectAssetsAtOrBefore_ExcludesLaterAssetsAndOrdersTheResult()
    {
        var referenceTime = new DateTimeOffset(2026, 8, 31, 6, 30, 0, TimeSpan.Zero);
        RadarAsset[] assets =
        [
            AssetAt(referenceTime.AddMinutes(5)),
            AssetAt(referenceTime),
            AssetAt(referenceTime.AddHours(-1))
        ];

        var selected = RadarUpdateService.SelectAssetsAtOrBefore(assets, referenceTime);

        Assert.Equal(
            [referenceTime.AddHours(-1), referenceTime],
            selected.Select(asset => asset.Timestamp));
    }

    [Fact]
    public void NormalizePeriods_RemovesDuplicatesAndOrdersValues()
    {
        var periods = RadarUpdateService.NormalizePeriods([24, 1, 3, 1, 24, 6]);

        Assert.Equal([1, 3, 6, 24], periods);
    }

    private static RadarAsset AssetAt(DateTimeOffset timestamp) =>
        new($"file-{timestamp:yyyyMMddHHmm}", new Uri("https://example.test/file"), timestamp);
}
