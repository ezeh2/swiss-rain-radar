using SwissRainRadar.Web.Services;

namespace SwissRainRadar.Tests;

public sealed class MeteoSwissClientTests
{
    [Fact]
    public void TryParseTimestamp_ParsesCpcFileNameAsUtc()
    {
        var success = MeteoSwissClient.TryParseTimestamp(
            "cpc2624306303_00060.001.h5",
            out var timestamp);

        Assert.True(success);
        Assert.Equal(new DateTimeOffset(2026, 8, 31, 6, 30, 0, TimeSpan.Zero), timestamp);
    }

    [Theory]
    [InlineData("rzc2624306300.801.h5")]
    [InlineData("cpc-invalid.h5")]
    [InlineData("")]
    public void TryParseTimestamp_RejectsNonCpcFiles(string fileName)
    {
        Assert.False(MeteoSwissClient.TryParseTimestamp(fileName, out _));
    }
}

