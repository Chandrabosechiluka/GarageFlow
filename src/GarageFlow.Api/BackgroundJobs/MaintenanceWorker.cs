using GarageFlow.Application.Services;

namespace GarageFlow.Api.BackgroundJobs;

/// <summary>
/// A hosted service: ASP.NET Core starts it with the app and stops it on shutdown. Every interval it runs each
/// maintenance job in its OWN scope (so each job gets a fresh DbContext) and in its own try/catch (so one failing job
/// never stops the others, and nothing ever stops the loop).
/// </summary>
public sealed class MaintenanceWorker(
    IServiceScopeFactory scopeFactory,
    MaintenanceSettings settings,
    ILogger<MaintenanceWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(5, settings.IntervalSeconds));
        logger.LogInformation("Maintenance worker started; running every {Interval}.", interval);

        // Let the app finish starting (migrations in Development) before the first run.
        try { await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); }
        catch (OperationCanceledException) { return; }

        using var timer = new PeriodicTimer(interval);
        do
        {
            await RunJobAsync("no-shows", (m, ct) => m.MarkNoShowsAsync(ct), stoppingToken);
            await RunJobAsync("reminders", (m, ct) => m.SendRemindersAsync(ct), stoppingToken);
            await RunJobAsync("offer notifications", (m, ct) => m.NotifyOffersAsync(ct), stoppingToken);
            await RunJobAsync("offer expiry", (m, ct) => m.ExpireOffersAsync(ct), stoppingToken);
        }
        while (await WaitForNextTickAsync(timer, stoppingToken));

        logger.LogInformation("Maintenance worker stopped.");
    }

    private async Task RunJobAsync(string name, Func<IMaintenanceService, CancellationToken, Task<int>> job, CancellationToken ct)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var count = await job(scope.ServiceProvider.GetRequiredService<IMaintenanceService>(), ct);
            if (count > 0)
                logger.LogInformation("Maintenance job '{Job}' handled {Count} item(s).", name, count);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // shutting down
        }
        catch (Exception ex)
        {
            // Never let one bad run kill the worker: log it and try again at the next tick.
            logger.LogError(ex, "Maintenance job '{Job}' failed; it will be retried at the next run.", name);
        }
    }

    private static async Task<bool> WaitForNextTickAsync(PeriodicTimer timer, CancellationToken ct)
    {
        try { return await timer.WaitForNextTickAsync(ct); }
        catch (OperationCanceledException) { return false; }
    }
}
