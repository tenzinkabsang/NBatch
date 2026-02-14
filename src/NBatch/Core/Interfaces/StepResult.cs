namespace NBatch.Core.Interfaces;

public record StepResult(string Name, bool Success, int ItemsRead = 0, int ItemsProcessed = 0, int ErrorsSkipped = 0);