namespace NBatch.Core.Repositories.Entities;

internal sealed class BatchStepEntity
{
    public long Id { get; set; }
    public string StepName { get; set; } = string.Empty;
    public string JobName { get; set; } = string.Empty;
    public long StepIndex { get; set; }
    public int NumberOfItemsProcessed { get; set; }
    public bool Error { get; set; }
    public bool Skipped { get; set; }
}
