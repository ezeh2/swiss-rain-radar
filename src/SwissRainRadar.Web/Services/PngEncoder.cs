using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace SwissRainRadar.Web.Services;

public static class PngEncoder
{
    private static readonly byte[] Signature = [137, 80, 78, 71, 13, 10, 26, 10];

    public static async Task<MemoryStream> EncodeRgbaAsync(
        int width,
        int height,
        byte[] rgba,
        CancellationToken cancellationToken)
    {
        if (rgba.Length != width * height * 4)
        {
            throw new ArgumentException("RGBA data length does not match the image dimensions.", nameof(rgba));
        }

        var output = new MemoryStream();
        await output.WriteAsync(Signature, cancellationToken);

        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(0, 4), width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4, 4), height);
        header[8] = 8;
        header[9] = 6;
        await WriteChunkAsync(output, "IHDR", header, cancellationToken);

        await using var compressed = new MemoryStream();
        await using (var zlib = new ZLibStream(compressed, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            var rowLength = width * 4;
            for (var row = 0; row < height; row++)
            {
                await zlib.WriteAsync(new byte[] { 0 }, cancellationToken);
                await zlib.WriteAsync(rgba.AsMemory(row * rowLength, rowLength), cancellationToken);
            }
        }

        await WriteChunkAsync(output, "IDAT", compressed.ToArray(), cancellationToken);
        await WriteChunkAsync(output, "IEND", [], cancellationToken);
        output.Position = 0;
        return output;
    }

    private static async Task WriteChunkAsync(
        Stream output,
        string type,
        byte[] data,
        CancellationToken cancellationToken)
    {
        var typeBytes = Encoding.ASCII.GetBytes(type);
        var length = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, data.Length);
        await output.WriteAsync(length, cancellationToken);
        await output.WriteAsync(typeBytes, cancellationToken);
        await output.WriteAsync(data, cancellationToken);

        var crcInput = new byte[typeBytes.Length + data.Length];
        typeBytes.CopyTo(crcInput, 0);
        data.CopyTo(crcInput, typeBytes.Length);
        var crcBytes = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crcBytes, ComputeCrc32(crcInput));
        await output.WriteAsync(crcBytes, cancellationToken);
    }

    private static uint ComputeCrc32(ReadOnlySpan<byte> data)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var value in data)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++)
            {
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
            }
        }

        return ~crc;
    }
}
