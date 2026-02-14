namespace NBatch.Core.Interfaces;

/// <summary>The result of a completed step.</summary>
/// <param name="Name">The step name.</param>
/// <param name="Success">Whether the step completed successfully.</param>
/// <param name="ItemsRead">Total items read by the reader.</param>
/// <param name="ItemsProcessed">Total items written successfully.</param>
/// <param name="ErrorsSkipped">Number of items skipped due to the skip policy.</param>
public record StepResult(string Name, bool Success, int ItemsRead = 0, int ItemsProcessed = 0, int ErrorsSkipped = 0);