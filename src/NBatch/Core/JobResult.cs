using NBatch.Core.Interfaces;

namespace NBatch.Core;

public record JobResult(string Name, bool Success, IReadOnlyList<StepResult> Steps);
