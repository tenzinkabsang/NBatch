namespace NBatch.Core.Interfaces;

/// <summary>
/// Receives callbacks before and after a step executes.
/// Implement only the methods you need — both have no-op defaults.
/// </summary>
public interface IStepListener
{
    Task BeforeStepAsync(string stepName, CancellationToken cancellationToken) => Task.CompletedTask;
    Task AfterStepAsync(StepResult result, CancellationToken cancellationToken) => Task.CompletedTask;
}
