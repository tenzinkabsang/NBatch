using NBatch.Core.Exceptions;
using NBatch.Core.Interfaces;

namespace NBatch.Core;

/// <summary>The result of a completed job, including per-step details.</summary>
/// <param name="Name">The job name.</param>
/// <param name="Success">Whether every executed step succeeded and the run was not cancelled.</param>
/// <param name="Steps">Results for each step in execution order.</param>
/// <param name="Cancelled">Whether the run was cancelled before completing.</param>
/// <param name="Duration">Wall-clock execution time of the run.</param>
public record JobResult(
    string Name,
    bool Success,
    IReadOnlyList<StepResult> Steps,
    bool Cancelled = false,
    TimeSpan Duration = default)
{
    /// <summary>
    /// Throws a <see cref="JobFailedException"/> if the job did not succeed;
    /// otherwise returns this result for chaining.
    /// </summary>
    /// <exception cref="JobFailedException">The run failed or was cancelled.</exception>
    public JobResult EnsureSuccess()
        => Success ? this : throw new JobFailedException(this);
}
