namespace SwissRainRadar.Web.Services;

public sealed class FixedTimeProvider(DateTimeOffset fixedUtcNow) : TimeProvider
{
    private readonly DateTimeOffset _fixedUtcNow = fixedUtcNow.ToUniversalTime();

    public override DateTimeOffset GetUtcNow() => _fixedUtcNow;
}
