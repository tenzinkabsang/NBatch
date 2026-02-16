namespace NBatch.Core;

/// <summary>
/// Runs registered batch jobs by name. Inject this into controllers,
/// hosted services, or any component that needs to trigger a job.
/// </summary>
public interface IJobRunner
{
    /// <summary>Runs the job registered with the given name.</summary>
    /// <param name="jobName">The name used when the job was registered via <c>AddJob</c>.</param>
    /// <param name="cancellationToken">Token to cancel the job.</param>
    /// <returns>The result of the completed job.</returns>
    /// <exception cref="ArgumentException">No job is registered with <paramref name="jobName"/>.</exception>
    Task<JobResult> RunAsync(string jobName, CancellationToken cancellationToken = default);
}
