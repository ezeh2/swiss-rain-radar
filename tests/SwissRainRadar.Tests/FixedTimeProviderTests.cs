using SwissRainRadar.Web.Services;

namespace SwissRainRadar.Tests;

public sealed class FixedTimeProviderTests
{
    [Fact]
    public void GetUtcNow_AlwaysReturnsConfiguredTimeInUtc()
    {
        var configuredTime = new DateTimeOffset(2026, 8, 31, 8, 30, 0, TimeSpan.FromHours(2));
        var provider = new FixedTimeProvider(configuredTime);

        var first = provider.GetUtcNow();
        var second = provider.GetUtcNow();

        Assert.Equal(new DateTimeOffset(2026, 8, 31, 6, 30, 0, TimeSpan.Zero), first);
        Assert.Equal(first, second);
        Assert.Equal(TimeSpan.Zero, first.Offset);
    }
}
