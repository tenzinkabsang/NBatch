namespace NBatch.Core.Interfaces;

/// <summary>
/// Receives callbacks before and after a job executes.
/// Implement only the methods you need — both have no-op defaults.
/// </summary>
public interface IJobListener
{
    /// <summary>Called before the job starts.</summary>
    Task BeforeJobAsync(string jobName, CancellationToken cancellationToken) => Task.CompletedTask;
    /// <summary>Called after the job completes.</summary>
    Task AfterJobAsync(JobResult result, CancellationToken cancellationToken) => Task.CompletedTask;
}
