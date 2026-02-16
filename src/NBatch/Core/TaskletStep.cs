using System.Runtime.ExceptionServices;
using Microsoft.Extensions.Logging;
using NBatch.Core.Interfaces;
using NBatch.Core.Repositories;

namespace NBatch.Core;

/// <summary>
/// A step that executes a single <see cref="ITasklet"/> unit of work
/// instead of the chunk-oriented Reader/Processor/Writer pipeline.
/// </summary>
internal sealed class TaskletStep(string stepName, ITasklet tasklet, IStepRepository stepRepository, ILogger logger) : IStep
{
    public string Name { get; } = stepName;

    public async Task<StepResult> ProcessAsync(CancellationToken cancellationToken = default)
    {
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

        long stepId = await stepRepository.InsertStepAsync(Name, 0, cancellationToken);
        await stepRepository.UpdateStepAsync(stepId, 0, error: failure is not null, skipped: false, cancellationToken);

        if (failure is not null)
        {
            logger.LogError(failure, "Tasklet '{StepName}' failed", Name);
            ExceptionDispatchInfo.Throw(failure);
        }

        logger.LogDebug("Tasklet '{StepName}' completed", Name);
        return new StepResult(Name, true);
    }
}
