namespace SwissRainRadar.Web.Models;

public sealed record MapManifest(
    DateTimeOffset UpdatedAt,
    DateTimeOffset PeriodEnd,
    IReadOnlyList<MapVariant> Maps,
    MapBounds Bounds,
    string Source,
    int AvailableRawFiles,
    int ExpectedRawFiles);

public sealed record MapVariant(int Hours, string ImageUrl);

public sealed record MapBounds(double South, double West, double North, double East);

