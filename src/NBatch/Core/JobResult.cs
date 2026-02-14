using NBatch.Core.Interfaces;

namespace NBatch.Core;

/// <summary>The result of a completed job, including per-step details.</summary>
/// <param name="Name">The job name.</param>
/// <param name="Success">Whether all steps completed successfully.</param>
/// <param name="Steps">Results for each step in execution order.</param>
public record JobResult(string Name, bool Success, IReadOnlyList<StepResult> Steps);
