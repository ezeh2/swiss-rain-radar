using SwissRainRadar.Web.Services;

namespace SwissRainRadar.Tests;

public sealed class PngEncoderTests
{
    [Fact]
    public async Task EncodeRgbaAsync_WritesPngSignatureAndEndChunk()
    {
        await using var png = await PngEncoder.EncodeRgbaAsync(1, 1, [255, 0, 0, 255], CancellationToken.None);
        var bytes = png.ToArray();

        Assert.Equal([137, 80, 78, 71, 13, 10, 26, 10], bytes[..8]);
        Assert.Equal("IEND", System.Text.Encoding.ASCII.GetString(bytes[^8..^4]));
    }
}
