namespace NBatch.Core.Interfaces;

/// <summary>
/// Receives callbacks before and after a step executes.
/// Implement only the methods you need — both have no-op defaults.
/// </summary>
public interface IStepListener
{
    /// <summary>Called before the step starts.</summary>
    Task BeforeStepAsync(string stepName, CancellationToken cancellationToken) => Task.CompletedTask;
    /// <summary>Called after the step completes.</summary>
    Task AfterStepAsync(StepResult result, CancellationToken cancellationToken) => Task.CompletedTask;
}
