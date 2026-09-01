using Microsoft.Extensions.Options;
using SwissRainRadar.Web.Options;

namespace SwissRainRadar.Web.Services;

public sealed partial class RadarUpdateWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<RadarOptions> options,
    ILogger<RadarUpdateWorker> logger) : BackgroundService
{
    private readonly RadarOptions _options = options.Value;
    private readonly TimeSpan _interval = TimeSpan.FromMinutes(options.Value.UpdateIntervalMinutes);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RunUpdateAsync(stoppingToken);

        if (_options.FixedReferenceTimeUtc is not null && _options.RunOnceWhenReferenceTimeIsFixed)
        {
            if (_options.BackfillOnStartup)
            {
                await RunBackfillAsync(stoppingToken);
            }

            LogFixedReferenceRunCompleted(_options.FixedReferenceTimeUtc.Value);
            return;
        }

        if (_options.BackfillOnStartup)
        {
            _ = Task.Run(() => RunBackfillAsync(stoppingToken), stoppingToken);
        }

        using var timer = new PeriodicTimer(_interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunUpdateAsync(stoppingToken);
        }
    }

    private async Task RunUpdateAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<RadarUpdateService>().UpdateLatestAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
        catch (Exception exception)
        {
            LogUpdateFailure(exception);
        }
    }

    private async Task RunBackfillAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<RadarUpdateService>().BackfillRawAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
        catch (Exception exception)
        {
            LogBackfillFailure(exception);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "The radar update failed; the previous map remains available.")]
    private partial void LogUpdateFailure(Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "The optional raw-data backfill failed.")]
    private partial void LogBackfillFailure(Exception exception);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Fixed reference time {referenceTime} processed; periodic radar updates are disabled.")]
    private partial void LogFixedReferenceRunCompleted(DateTimeOffset referenceTime);
}
