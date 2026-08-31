using SwissRainRadar.Web.Services;

namespace SwissRainRadar.Tests;

public sealed class RadarColorScaleTests
{
    [Fact]
    public void GetColor_IsTransparentForDryCells()
    {
        Assert.Equal(new RgbaColor(0, 0, 0, 0), RadarColorScale.GetColor(0));
    }

    [Fact]
    public void GetColor_UsesExpectedHeavyRainClass()
    {
        Assert.Equal(new RgbaColor(255, 0, 0, 220), RadarColorScale.GetColor(175));
    }
}
