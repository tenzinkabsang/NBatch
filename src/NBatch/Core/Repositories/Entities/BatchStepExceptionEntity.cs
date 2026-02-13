namespace NBatch.Core.Repositories.Entities;

internal sealed class BatchStepExceptionEntity
{
    public long Id { get; set; }
    public long StepIndex { get; set; }
    public string StepName { get; set; } = string.Empty;
    public string JobName { get; set; } = string.Empty;
    public string? ExceptionMsg { get; set; }
    public string? ExceptionDetails { get; set; }
    public DateTime CreateDate { get; set; } = DateTime.UtcNow;
}
