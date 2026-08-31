using SwissRainRadar.Web.Models;

namespace SwissRainRadar.Web.Services;

public sealed class RadarImageRenderer
{
    private const int OutputWidth = 1000;
    private const int OutputHeight = 600;
    private const double West = 2.68942;
    private const double East = 12.4623;
    private const double South = 43.619;
    private const double North = 49.3744;
    private const double SourceWestLv95 = 2_255_000;
    private const double SourceNorthLv95 = 1_480_000;
    private const double CellSizeMetres = 1_000;

    public async Task<MemoryStream> RenderAsync(RadarGrid grid, CancellationToken cancellationToken)
    {
        var pixels = new byte[OutputWidth * OutputHeight * 4];
        for (var y = 0; y < OutputHeight; y++)
        {
            var latitude = North - ((North - South) * y / (OutputHeight - 1));
            for (var x = 0; x < OutputWidth; x++)
            {
                var longitude = West + ((East - West) * x / (OutputWidth - 1));
                var (easting, northing) = SwissProjection.ToLv95(latitude, longitude);
                var sourceX = (int)Math.Round((easting - SourceWestLv95) / CellSizeMetres);
                var sourceY = (int)Math.Round((SourceNorthLv95 - northing) / CellSizeMetres);

                var color = sourceX >= 0 && sourceX < grid.Width && sourceY >= 0 && sourceY < grid.Height
                    ? RadarColorScale.GetColor(grid.Values[(sourceY * grid.Width) + sourceX])
                    : new RgbaColor(0, 0, 0, 0);
                var index = ((y * OutputWidth) + x) * 4;
                pixels[index] = color.Red;
                pixels[index + 1] = color.Green;
                pixels[index + 2] = color.Blue;
                pixels[index + 3] = color.Alpha;
            }
        }

        return await PngEncoder.EncodeRgbaAsync(OutputWidth, OutputHeight, pixels, cancellationToken);
    }
}
