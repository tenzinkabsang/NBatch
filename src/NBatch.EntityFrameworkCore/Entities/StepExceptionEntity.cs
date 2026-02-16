namespace NBatch.Core.Repositories.Entities;

internal sealed class StepExceptionEntity
{
    public long Id { get; set; }
    public long StepIndex { get; set; }
    public string StepName { get; set; } = string.Empty;
    public string JobName { get; set; } = string.Empty;
    public long ExecutionId { get; set; }
    public string? ExceptionMsg { get; set; }
    public string? ExceptionDetails { get; set; }
    public DateTime CreateDate { get; set; } = DateTime.UtcNow;
}
