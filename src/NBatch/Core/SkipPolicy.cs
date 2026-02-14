using NBatch.Core.Repositories;

namespace NBatch.Core;

/// <summary>
/// Configure exceptions that are safe to skip. Allows the Job to continue processing the next item.
/// </summary>
public sealed class SkipPolicy
{
    private readonly int _skipLimit;
    private readonly Type[] _skippableExceptions;

    public SkipPolicy(Type[] skippableExceptions, int skipLimit)
    {
        ArgumentNullException.ThrowIfNull(skippableExceptions);

        if (!skippableExceptions.All(IsExceptionType))
            throw new ArgumentException("All types must derive from Exception.", nameof(skippableExceptions));

        _skippableExceptions = skippableExceptions;
        _skipLimit = skipLimit;
    }

    internal async Task<bool> IsSatisfiedByAsync(IStepRepository stepRepository, SkipContext skipContext, CancellationToken cancellationToken = default)
    {
        if (_skippableExceptions.Length == 0 || _skipLimit == 0)
            return false;

        int exceptionCount = await stepRepository.GetExceptionCountAsync(skipContext, cancellationToken);

        if (exceptionCount >= _skipLimit || !_skippableExceptions.Contains(skipContext.ExceptionType))
            return false;

        await stepRepository.SaveExceptionInfoAsync(skipContext, exceptionCount, cancellationToken);
        return true;
    }

    private static bool IsExceptionType(Type type)
        => type == typeof(Exception) || type.IsSubclassOf(typeof(Exception));

    public static SkipPolicy None => new(skippableExceptions: [], skipLimit: 0);

    public static SkipPolicy For<TException>(int maxSkips) where TException : Exception
        => new([typeof(TException)], maxSkips);

    public static SkipPolicy For<TException1, TException2>(int maxSkips)
        where TException1 : Exception
        where TException2 : Exception
        => new([typeof(TException1), typeof(TException2)], maxSkips);

    public static SkipPolicy For<TException1, TException2, TException3>(int maxSkips)
        where TException1 : Exception
        where TException2 : Exception
        where TException3 : Exception
        => new([typeof(TException1), typeof(TException2), typeof(TException3)], maxSkips);
}
