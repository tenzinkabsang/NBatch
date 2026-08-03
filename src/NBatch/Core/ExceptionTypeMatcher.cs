namespace NBatch.Core;

/// <summary>
/// Shared exception-matching rules for <see cref="SkipPolicy"/> and <see cref="RetryPolicy"/>:
/// a thrown exception matches when it — or anything in its inner-exception chain
/// (bounded depth) — is assignable to one of the candidate types.
/// </summary>
internal static class ExceptionTypeMatcher
{
    private const int MaxInnerExceptionDepth = 10;

    public static bool Matches(Type[] candidateTypes, Exception exception)
    {
        var current = exception;
        for (int depth = 0; current is not null && depth <= MaxInnerExceptionDepth; depth++)
        {
            var thrownType = current.GetType();
            foreach (var candidate in candidateTypes)
            {
                if (candidate.IsAssignableFrom(thrownType))
                    return true;
            }
            current = current.InnerException;
        }

        return false;
    }
}
