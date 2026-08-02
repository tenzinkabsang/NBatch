namespace NBatch.Core.Repositories.Entities;

internal sealed class JobEntity
{
    public string JobName { get; set; } = string.Empty;
    public DateTime CreateDate { get; set; } = DateTime.UtcNow;
    public DateTime LastRun { get; set; }

    /// <summary>
    /// Outcome of the most recent run: true = completed successfully (next run resets),
    /// false = failed or cancelled (next run resumes), null = in flight or crashed (next run resumes).
    /// </summary>
    public bool? LastRunSuccess { get; set; }
}
