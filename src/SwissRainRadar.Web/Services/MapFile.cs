using System.Globalization;
using System.Text.Json;
using SwissRainRadar.Web.Models;

namespace SwissRainRadar.Web.Services;

/// <summary>
/// Defines naming, parsing, URL, and serialization conventions for generated map files.
/// Input: PNG map identities or object-store paths. Output: normalized PNG paths, API URLs,
/// or parsed map metadata; this helper does not read or write files itself.
/// </summary>
internal static class MapFile
{
    public static JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static string TimestampPrefix(DateTimeOffset periodEnd) => $"history/{periodEnd:yyyyMMddHHmm}/";

    public static string Path(DateTimeOffset periodEnd, int hours) => $"{TimestampPrefix(periodEnd)}{hours}h.png";

    public static MapVariant Variant(DateTimeOffset periodEnd, int hours) =>
        new(hours, $"/api/maps/{periodEnd:yyyyMMddHHmm}/{hours}");

    public static (DateTimeOffset PeriodEnd, int Hours)? TryParse(string path)
    {
        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3
            || parts[0] != "history"
            || !DateTimeOffset.TryParseExact(
                parts[1],
                "yyyyMMddHHmm",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var periodEnd)
            || !parts[2].EndsWith("h.png", StringComparison.Ordinal)
            || !int.TryParse(parts[2][..^5], CultureInfo.InvariantCulture, out var hours))
        {
            return null;
        }

        return (periodEnd, hours);
    }
}
