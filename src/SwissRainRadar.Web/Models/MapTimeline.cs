namespace SwissRainRadar.Web.Models;

public sealed record MapTimeline(IReadOnlyList<MapSnapshot> Snapshots);

public sealed record MapSnapshot(
    DateTimeOffset PeriodEnd,
    IReadOnlyList<MapVariant> Maps);
