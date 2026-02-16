using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace NBatch.Core;

/// <summary>
/// A <see cref="BackgroundService"/> that runs a single NBatch job on a configured schedule.
/// One instance is registered per scheduled job.
/// </summary>
internal sealed class NBatchJobWorkerService(
    IJobRunner jobRunner,
    string jobName,
    JobRegistration registration,
    ILogger logger) : BackgroundService
{
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

        // RunEvery: execute immediately, then wait the configured interval after each completion.
        while (!stoppingToken.IsCancellationRequested)
        {
            await RunJobAsync(stoppingToken);
            await Task.Delay(registration.Interval, stoppingToken);
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
            // The next interval will retry. OperationCanceledException is
            // excluded so graceful shutdown propagates correctly.
            logger.LogError(ex,
                "NBatch worker job '{JobName}' failed — will retry after interval",
                jobName);
        }
    }
}
