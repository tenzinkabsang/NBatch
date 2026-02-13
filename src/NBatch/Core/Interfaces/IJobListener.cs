namespace NBatch.Core.Interfaces;

/// <summary>
/// Receives callbacks before and after a job executes.
/// Implement only the methods you need — both have no-op defaults.
/// </summary>
public interface IJobListener
{
    Task BeforeJobAsync(string jobName) => Task.CompletedTask;
    Task AfterJobAsync(JobResult result) => Task.CompletedTask;
}
