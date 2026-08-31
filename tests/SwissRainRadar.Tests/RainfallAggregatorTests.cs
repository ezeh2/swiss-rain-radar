using SwissRainRadar.Web.Models;
using SwissRainRadar.Web.Services;

namespace SwissRainRadar.Tests;

public sealed class RainfallAggregatorTests
{
    [Fact]
    public void Sum_AddsEveryCellWithoutMutatingSources()
    {
        var first = new RadarGrid(2, 2, [1f, 2f, 3f, 4f]);
        var second = new RadarGrid(2, 2, [0.5f, 1f, 1.5f, 2f]);

        var result = new RainfallAggregator().Sum([first, second]);

        Assert.Equal([1.5f, 3f, 4.5f, 6f], result.Values);
        Assert.Equal(1f, first.Values[0]);
    }

    [Fact]
    public void Sum_RejectsDifferentGridSizes()
    {
        var first = new RadarGrid(2, 1, [1f, 2f]);
        var second = new RadarGrid(1, 1, [1f]);

        Assert.Throws<InvalidDataException>(() => new RainfallAggregator().Sum([first, second]));
    }
}

