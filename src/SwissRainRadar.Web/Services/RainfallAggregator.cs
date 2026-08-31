using SwissRainRadar.Web.Models;

namespace SwissRainRadar.Web.Services;

public sealed class RainfallAggregator
{
    public RadarGrid Sum(IReadOnlyList<RadarGrid> grids)
    {
        ArgumentNullException.ThrowIfNull(grids);
        if (grids.Count == 0)
        {
            throw new ArgumentException("At least one grid is required.", nameof(grids));
        }

        var width = grids[0].Width;
        var height = grids[0].Height;
        var sum = new float[width * height];

        foreach (var grid in grids)
        {
            if (grid.Width != width || grid.Height != height)
            {
                throw new InvalidDataException("All radar grids must have identical dimensions.");
            }

            for (var index = 0; index < sum.Length; index++)
            {
                sum[index] += grid.Values[index];
            }
        }

        return new RadarGrid(width, height, sum);
    }
}

