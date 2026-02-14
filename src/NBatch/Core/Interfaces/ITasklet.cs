namespace NBatch.Core.Interfaces;

/// <summary>
/// A single unit-of-work step that does not follow the Reader/Processor/Writer pattern.
/// Use for cleanup tasks, sending notifications, running stored procedures, etc.
/// </summary>
public interface ITasklet
{
    Task ExecuteAsync(CancellationToken cancellationToken = default);
}
