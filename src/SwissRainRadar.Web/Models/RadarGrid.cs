namespace SwissRainRadar.Web.Models;

public sealed class RadarGrid
{
    public const int ExpectedWidth = 710;
    public const int ExpectedHeight = 640;

    public RadarGrid(int width, int height, float[] values)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        if (values.Length != width * height)
        {
            throw new ArgumentException("Grid data length does not match its dimensions.", nameof(values));
        }

        Width = width;
        Height = height;
        Values = values;
    }

    public int Width { get; }

    public int Height { get; }

    public float[] Values { get; }
}

