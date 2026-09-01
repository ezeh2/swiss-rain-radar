namespace SwissRainRadar.Web.Options;

public sealed class RadarOptions
{
    public const string SectionName = "Radar";

    public required Uri StacBaseUrl { get; init; }

    public int UpdateIntervalMinutes { get; init; } = 5;

    public int RawRetentionDays { get; init; } = 14;

    public bool BackfillOnStartup { get; init; } = true;

    public DateTimeOffset? FixedReferenceTimeUtc { get; init; }

    public bool RunOnceWhenReferenceTimeIsFixed { get; init; } = true;

    public int[] PeriodsHours { get; init; } = [1, 3, 6, 12, 24];
}
