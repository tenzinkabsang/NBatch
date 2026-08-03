namespace NBatch.Core.Interfaces;

/// <summary>The result of a completed step.</summary>
/// <param name="Name">The step name.</param>
/// <param name="Success">Whether the step completed successfully.</param>
/// <param name="ItemsRead">Total items read by the reader.</param>
/// <param name="ItemsWritten">Total items written successfully.</param>
/// <param name="ItemsSkipped">Individual items skipped via the skip policy.</param>
/// <param name="Duration">Wall-clock execution time of the step.</param>
public record StepResult(
    string Name,
    bool Success,
    int ItemsRead = 0,
    int ItemsWritten = 0,
    int ItemsSkipped = 0,
    TimeSpan Duration = default);
