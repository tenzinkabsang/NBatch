namespace NBatch.Core.Interfaces;

/// <summary>Represents a single executable step within a job.</summary>
public interface IStep
{
    /// <summary>The unique name of this step.</summary>
    string Name { get; }
    /// <summary>Executes the step and returns its result.</summary>
    Task<StepResult> ProcessAsync(CancellationToken cancellationToken = default);
}
