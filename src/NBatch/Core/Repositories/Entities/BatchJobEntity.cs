namespace NBatch.Core.Repositories.Entities;

internal sealed class BatchJobEntity
{
    public string JobName { get; set; } = string.Empty;
    public DateTime CreateDate { get; set; } = DateTime.UtcNow;
    public DateTime LastRun { get; set; }
}
