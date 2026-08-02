using System.Runtime.ExceptionServices;
using Microsoft.Extensions.Logging;
using NBatch.Core.Interfaces;
using NBatch.Core.Repositories;

namespace NBatch.Core;

/// <summary>
/// A step that executes a single <see cref="ITasklet"/> unit of work
/// instead of the chunk-oriented Reader/Processor/Writer pipeline.
/// Completion is tracked so a tasklet that already succeeded is not re-executed
/// when the job resumes after a later step's failure; after a fully successful
/// run the job store resets and the tasklet runs again on the next run.
/// </summary>
internal sealed class TaskletStep(string stepName, ITasklet tasklet, IStepRepository stepRepository, ILogger logger) : IStep
{
    /// <summary>Sentinel step index recorded once the tasklet has executed (0 = pending).</summary>
    private const long CompletedIndex = 1;

    public string Name { get; } = stepName;

    public async Task<StepResult> ProcessAsync(CancellationToken cancellationToken = default)
    {
        var previous = await stepRepository.GetStartIndexAsync(Name, cancellationToken);
        if (previous.StepIndex >= CompletedIndex && !previous.Error)
        {
            logger.LogInformation("Tasklet '{StepName}' already completed in a previous run — skipping", Name);
            return new StepResult(Name, true);
        }

        Exception? failure = null;
        try
        {
            await tasklet.ExecuteAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            failure = ex;
        }
        finally
        {
            if (tasklet is IAsyncDisposable asyncDisposable)
                await asyncDisposable.DisposeAsync();
            else if (tasklet is IDisposable disposable)
                disposable.Dispose();
        }

        // Bookkeeping uses a non-cancellable token: the outcome must be recorded
        // even when the run is being cancelled.
        long stepId = await stepRepository.InsertStepAsync(Name, CompletedIndex, CancellationToken.None);
        await stepRepository.UpdateStepAsync(stepId, 0, error: failure is not null, skipped: false, CancellationToken.None);

        if (failure is not null)
        {
            logger.LogError(failure, "Tasklet '{StepName}' failed", Name);
            ExceptionDispatchInfo.Throw(failure);
        }

        logger.LogDebug("Tasklet '{StepName}' completed", Name);
        return new StepResult(Name, true);
    }
}
