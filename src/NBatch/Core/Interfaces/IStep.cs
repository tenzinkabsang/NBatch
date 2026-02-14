namespace NBatch.Core.Interfaces;

public interface IStep
{
    string Name { get; }
    Task<StepResult> ProcessAsync(CancellationToken cancellationToken = default);
}
