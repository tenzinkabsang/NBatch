namespace NBatch.Core;

public sealed record SkipContext
{
    private readonly Exception _exception;
    public string StepName { get; init; }
    public long StepIndex { get; init; }
    public Type ExceptionType => _exception.GetType();
    public string ExceptionMessage => _exception.Message;
    public string ExceptionDetail => _exception.StackTrace!;

    public SkipContext(string stepName, long stepIndex, Exception exception)
    {
        StepName = stepName;
        StepIndex = stepIndex;
        _exception = exception;
    }
}
