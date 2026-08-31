namespace SwissRainRadar.Web.Options;

public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    public string? AccountUri { get; init; }

    public string LocalRoot { get; init; } = "App_Data";
}

