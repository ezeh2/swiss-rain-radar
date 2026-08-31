using PureHDF;
using SwissRainRadar.Web.Models;
using System.Globalization;

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

            static bool TryReadGeneric<T>(PureHDF.IH5Dataset dataset, out float[] result)
            {
                try
                {
                    var arr = dataset.Read<T[]>();
                    result = Array.ConvertAll(arr, item => Convert.ToSingle(item, CultureInfo.InvariantCulture));
                    return true;
                }
                catch
                {
                    result = null!;
                    return false;
                }
            }

            if (!TryReadGeneric<float>(dataset, out values)
                && !TryReadGeneric<double>(dataset, out values)
                && !TryReadGeneric<int>(dataset, out values)
                && !TryReadGeneric<uint>(dataset, out values)
                && !TryReadGeneric<short>(dataset, out values)
                && !TryReadGeneric<ushort>(dataset, out values)
                && !TryReadGeneric<byte>(dataset, out values)
                && !TryReadGeneric<sbyte>(dataset, out values)
                && !TryReadGeneric<long>(dataset, out values)
                && !TryReadGeneric<ulong>(dataset, out values))
            {
                throw new InvalidDataException("Unable to read dataset as a supported numeric type.");
            }
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
