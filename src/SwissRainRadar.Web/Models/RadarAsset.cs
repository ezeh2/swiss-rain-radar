namespace SwissRainRadar.Web.Models;

public sealed record RadarAsset(string Name, Uri DownloadUri, DateTimeOffset Timestamp);

