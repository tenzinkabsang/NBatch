using NBatch.Core.Interfaces;
using NBatch.Core.Repositories;

namespace NBatch.Core;

/// <summary>
/// A step that executes a single <see cref="ITasklet"/> unit of work
/// instead of the chunk-oriented Reader/Processor/Writer pipeline.
/// </summary>
internal sealed class TaskletStep(string stepName, ITasklet tasklet) : IStep
{
    public string Name { get; } = stepName;

    public async Task<StepResult> ProcessAsync(StepContext stepContext, IStepRepository stepRepository)
    {
        long stepId = await stepRepository.InsertStepAsync(stepName, 0);
        try
        {
            await tasklet.ExecuteAsync();
            await stepRepository.UpdateStepAsync(stepId, 0, error: false, skipped: false);
            return new StepResult(Name, true);
        }
        catch
        {
            await stepRepository.UpdateStepAsync(stepId, 0, error: true, skipped: false);
            throw;
        }
    }
}
