namespace SwissRainRadar.Web.Services;

public static class RadarColorScale
{
    private static readonly (float Minimum, RgbaColor Color)[] Stops =
    [
        (450f, new RgbaColor(136, 0, 255, 220)),
        (400f, new RgbaColor(182, 0, 137, 220)),
        (350f, new RgbaColor(255, 0, 255, 220)),
        (300f, new RgbaColor(255, 75, 255, 220)),
        (250f, new RgbaColor(139, 0, 0, 220)),
        (200f, new RgbaColor(181, 0, 0, 220)),
        (150f, new RgbaColor(255, 0, 0, 220)),
        (100f, new RgbaColor(216, 107, 0, 220)),
        (70f, new RgbaColor(255, 131, 0, 220)),
        (40f, new RgbaColor(255, 166, 0, 220)),
        (20f, new RgbaColor(255, 200, 9, 220)),
        (10f, new RgbaColor(255, 255, 0, 210)),
        (7f, new RgbaColor(0, 135, 0, 205)),
        (4f, new RgbaColor(0, 185, 0, 200)),
        (2f, new RgbaColor(0, 230, 0, 195)),
        (1f, new RgbaColor(175, 255, 97, 190)),
        (0.5f, new RgbaColor(49, 97, 255, 180)),
        (0.2f, new RgbaColor(115, 148, 255, 165)),
        (0.01f, new RgbaColor(169, 189, 255, 145))
    ];

    public static RgbaColor GetColor(float millimetres)
    {
        foreach (var (minimum, color) in Stops)
        {
            if (millimetres >= minimum)
            {
                return color;
            }
        }

        return new RgbaColor(0, 0, 0, 0);
    }
}

public readonly record struct RgbaColor(byte Red, byte Green, byte Blue, byte Alpha);
