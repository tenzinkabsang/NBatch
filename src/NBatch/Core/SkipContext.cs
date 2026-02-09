namespace NBatch.Core;

public sealed record SkipContext(string StepName, long StepIndex, Exception Exception)
{
    public Type ExceptionType => Exception.GetType();
    public string ExceptionMessage => Exception.Message;
    public string ExceptionDetail => Exception.StackTrace!;
}
