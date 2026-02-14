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
        long stepId = await stepRepository.InsertStepAsync(Name, 0, cancellationToken);
        try
        {
            await tasklet.ExecuteAsync(cancellationToken);
            await stepRepository.UpdateStepAsync(stepId, 0, error: false, skipped: false, cancellationToken);
            logger.LogDebug("Tasklet '{StepName}' completed", Name);
            return new StepResult(Name, true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Tasklet '{StepName}' failed", Name);
            await stepRepository.UpdateStepAsync(stepId, 0, error: true, skipped: false, cancellationToken);
            throw;
        }
    }
}
