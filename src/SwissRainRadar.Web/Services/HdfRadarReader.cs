using PureHDF;
using SwissRainRadar.Web.Models;

namespace SwissRainRadar.Web.Services;

public sealed class HdfRadarReader
{
    public RadarGrid Read(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var temporaryPath = Path.Combine(Path.GetTempPath(), $"srr-{Guid.NewGuid():N}.h5");
        float[] values;
        try
        {
            using (var temporaryFile = File.Create(temporaryPath))
            {
                stream.CopyTo(temporaryFile);
            }

            using var file = H5File.OpenRead(temporaryPath);
            var dataset = file.Dataset("/dataset1/data1/data");
            values = dataset.Read<float[]>();
        }
        finally
        {
            File.Delete(temporaryPath);
        }

        if (values.Length != RadarGrid.ExpectedWidth * RadarGrid.ExpectedHeight)
        {
            throw new InvalidDataException(
                $"Unexpected CPC grid size {values.Length}; expected "
                + $"{RadarGrid.ExpectedWidth * RadarGrid.ExpectedHeight} values.");
        }

        for (var index = 0; index < values.Length; index++)
        {
            if (!float.IsFinite(values[index]) || values[index] < 0)
            {
                values[index] = 0;
            }
        }

        return new RadarGrid(RadarGrid.ExpectedWidth, RadarGrid.ExpectedHeight, values);
    }
}
