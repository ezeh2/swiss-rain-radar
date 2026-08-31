using SwissRainRadar.Web.Services;

namespace SwissRainRadar.Tests;

public sealed class SwissProjectionTests
{
    [Fact]
    public void ToLv95_MapsBernReferencePoint()
    {
        var (easting, northing) = SwissProjection.ToLv95(46.95108, 7.43864);

        Assert.InRange(easting, 2_599_900, 2_600_100);
        Assert.InRange(northing, 1_199_900, 1_200_100);
    }
}
