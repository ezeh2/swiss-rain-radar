using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using SwissRainRadar.Web.Models;
using SwissRainRadar.Web.Options;
using SwissRainRadar.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddOptions<RadarOptions>()
    .Bind(builder.Configuration.GetSection(RadarOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(options => options.UpdateIntervalMinutes >= 5, "Update interval must be at least five minutes.")
    .Validate(options => options.RawRetentionDays is >= 2 and <= 30, "Raw retention must be between 2 and 30 days.")
    .Validate(options => options.PeriodsHours.Length > 0 && options.PeriodsHours.All(period => period is >= 1 and <= 24),
        "Periods must contain values between 1 and 24 hours.")
    .ValidateOnStart();
builder.Services
    .AddOptions<StorageOptions>()
    .Bind(builder.Configuration.GetSection(StorageOptions.SectionName))
    .ValidateOnStart();

builder.Services.AddHttpClient<MeteoSwissClient>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(60);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("SwissRainRadar/1.0 (+https://github.com/ezeh2/swiss-rain-radar)");
});
builder.Services.AddHealthChecks();
builder.Services.AddSingleton<HdfRadarReader>();
builder.Services.AddSingleton<RainfallAggregator>();
builder.Services.AddSingleton<RadarImageRenderer>();
builder.Services.AddScoped<RadarUpdateService>();
builder.Services.AddHostedService<RadarUpdateWorker>();

var storageAccountUri = builder.Configuration[$"{StorageOptions.SectionName}:AccountUri"];
if (string.IsNullOrWhiteSpace(storageAccountUri))
{
    builder.Services.AddSingleton<IObjectStore, FileObjectStore>();
}
else
{
    builder.Services.AddSingleton<IObjectStore, BlobObjectStore>();
}

var app = builder.Build();

app.UseExceptionHandler("/error");
app.UseHsts();
app.UseHttpsRedirection();
app.Use(async (context, next) =>
{
    context.Response.Headers.ContentSecurityPolicy =
        "default-src 'self'; img-src 'self' data: https://*.tile.openstreetmap.org; "
        + "style-src 'self'; script-src 'self'; connect-src 'self'; frame-ancestors 'none'; base-uri 'self'";
    context.Response.Headers.XContentTypeOptions = "nosniff";
    // Allow the browser to send a Referer for cross-origin image requests
    // so external tile/image providers that require a referrer don't block access.
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    context.Response.Headers.Append("Permissions-Policy", "geolocation=(), camera=(), microphone=()");
    await next();
});
app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = context =>
    {
        context.Context.Response.Headers.CacheControl = context.File.Name.Contains("latest", StringComparison.OrdinalIgnoreCase)
            ? "no-cache"
            : "public,max-age=604800,immutable";
    }
});

app.MapHealthChecks("/healthz", new HealthCheckOptions
{
    ResponseWriter = static async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new { status = report.Status.ToString() });
    }
});

app.MapGet("/api/maps/latest", async (IObjectStore store, CancellationToken cancellationToken) =>
{
    var json = await store.ReadTextAsync("maps", "latest.json", cancellationToken);
    return json is null
        ? Results.Problem("No radar map has been generated yet.", statusCode: StatusCodes.Status503ServiceUnavailable)
        : Results.Text(json, "application/json");
}).WithName("LatestMap");

app.MapGet("/api/maps/{timestamp}/{hours:int}", async (
    string timestamp,
    int hours,
    IOptions<RadarOptions> options,
    IObjectStore store,
    CancellationToken cancellationToken) =>
{
    if (timestamp.Length != 12
        || !timestamp.All(char.IsAsciiDigit)
        || !options.Value.PeriodsHours.Contains(hours))
    {
        return Results.BadRequest();
    }

    var stream = await store.OpenReadAsync("maps", $"history/{timestamp}/{hours}h.png", cancellationToken);
    return stream is null
        ? Results.NotFound()
        : Results.Stream(stream, "image/png", enableRangeProcessing: false);
}).WithName("MapImage");

app.MapGet("/error", () => Results.Problem("An unexpected error occurred."));
app.MapFallbackToFile("index.html");

app.Run();

public partial class Program;
