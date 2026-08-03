namespace NBatch.Core.Exceptions;

/// <summary>
/// Thrown by <see cref="JobResult.EnsureSuccess"/> when a job run did not succeed.
/// </summary>
public sealed class JobFailedException : Exception
{
    /// <summary>The result of the failed run, including per-step details.</summary>
    public JobResult Result { get; }

    /// <summary>Initializes a new instance for the given failed result.</summary>
    /// <param name="result">The result of the failed run.</param>
    public JobFailedException(JobResult result) : base(BuildMessage(result))
    {
        ArgumentNullException.ThrowIfNull(result);
        Result = result;
    }

    private static string BuildMessage(JobResult result)
    {
        if (result.Cancelled)
            return $"Job '{result.Name}' was cancelled before completing.";

        var failedStep = result.Steps.FirstOrDefault(s => !s.Success);
        return failedStep is not null
            ? $"Job '{result.Name}' failed at step '{failedStep.Name}'."
            : $"Job '{result.Name}' did not complete successfully.";
    }
}
