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
}

