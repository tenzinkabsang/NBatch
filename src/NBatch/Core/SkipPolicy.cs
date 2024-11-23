using NBatch.Core.Repositories;

namespace NBatch.Core;

/// <summary>
/// Configure expections that are safe to skip. Allows the Job to continue processing the next item.
/// </summary>
public sealed class SkipPolicy
{
    public readonly int _skipLimit;
    private readonly Type[] _skippableExceptions;

    public SkipPolicy(Type[] skippableExceptions, int skipLimit)
    {
        ArgumentNullException.ThrowIfNull(skippableExceptions);
        _skippableExceptions = skippableExceptions;
        if (_skippableExceptions.Length > 0 && !_skippableExceptions.All(ex => ex.IsSubclassOf(typeof(Exception)) || ex == typeof(Exception)))
            throw new Exception("Invalid Skippable Exception Type");
        _skipLimit = skipLimit;
    }

    public async Task<bool> IsSatisfiedByAsync(IStepRepository stepRepository, SkipContext skipContext)
    {
        if (_skippableExceptions.Length == 0 || _skipLimit == 0 )
            return false;

        int exceptionCount = await stepRepository.GetExceptionCountAsync(skipContext);

        if (exceptionCount >= _skipLimit || !_skippableExceptions.Contains(skipContext.ExceptionType))
            return false;

        await stepRepository.SaveExceptionInfoAsync(skipContext, exceptionCount);
        return true;
    }

    public static SkipPolicy None => new(skippableExceptions: [], skipLimit: 0);
}
