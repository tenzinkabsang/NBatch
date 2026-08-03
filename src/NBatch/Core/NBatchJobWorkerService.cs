using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace NBatch.Core;

/// <summary>
/// A <see cref="BackgroundService"/> that runs a single NBatch job on a configured schedule
/// (run-once, fixed interval, or cron). One instance is registered per scheduled job.
/// </summary>
internal sealed class NBatchJobWorkerService(
    IJobRunner jobRunner,
    string jobName,
    JobRegistration registration,
    ILogger logger,
    TimeProvider timeProvider) : BackgroundService
{
    /// <summary>Long waits are chunked so cron gaps never exceed Task.Delay's ~49-day limit.</summary>
    private static readonly TimeSpan MaxDelayChunk = TimeSpan.FromDays(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Yield immediately so the host can finish starting all hosted services.
        // Without this, synchronous work in RunAsync blocks startup of subsequent services.
        await Task.Yield();

        logger.LogInformation("NBatch worker for job '{JobName}' started", jobName);

        if (registration.IsRunOnce)
        {
            await RunJobAsync(stoppingToken);
            logger.LogInformation("NBatch worker for job '{JobName}' completed (run-once)", jobName);
            return;
        }

        if (registration.CronSchedule is { } cron)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                // Computed from *now* after each run: occurrences that passed while
                // a run was in progress are skipped rather than executed back-to-back.
                var next = cron.GetNextOccurrence(timeProvider.GetUtcNow(), registration.TimeZone);
                if (next is null)
                {
                    logger.LogWarning("NBatch worker for job '{JobName}' — cron schedule has no future occurrences, worker exiting", jobName);
                    return;
                }

                logger.LogDebug("NBatch worker for job '{JobName}' — next cron occurrence at {Next:O}", jobName, next.Value);
                await DelayUntilAsync(next.Value, stoppingToken);
                await RunJobAsync(stoppingToken);
            }

            return;
        }

        // RunEvery: run immediately by default, or wait one interval first.
        if (!registration.RunImmediately)
            await Task.Delay(registration.Interval, timeProvider, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            await RunJobAsync(stoppingToken);
            await Task.Delay(registration.Interval, timeProvider, stoppingToken);
        }
    }

    private async Task DelayUntilAsync(DateTimeOffset target, CancellationToken cancellationToken)
    {
        while (true)
        {
            var remaining = target - timeProvider.GetUtcNow();
            if (remaining <= TimeSpan.Zero)
                return;

            await Task.Delay(remaining < MaxDelayChunk ? remaining : MaxDelayChunk, timeProvider, cancellationToken);
        }
    }

    private async Task RunJobAsync(CancellationToken ct)
    {
        try
        {
            logger.LogDebug("NBatch worker executing job '{JobName}'", jobName);
            var result = await jobRunner.RunAsync(jobName, ct);
            logger.LogInformation(
                "NBatch worker job '{JobName}' completed — success: {Success}",
                jobName, result.Success);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Log but don't crash the hosted service.
            // The next scheduled occurrence will retry. OperationCanceledException is
            // excluded so graceful shutdown propagates correctly.
            logger.LogError(ex,
                "NBatch worker job '{JobName}' failed — will retry at the next scheduled occurrence",
                jobName);
        }
    }
}
