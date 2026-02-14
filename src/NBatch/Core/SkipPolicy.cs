using NBatch.Core.Repositories;

namespace NBatch.Core;

/// <summary>
/// Configure exceptions that are safe to skip. Allows the Job to continue processing the next item.
/// </summary>
public sealed class SkipPolicy
{
    private readonly int _skipLimit;
    private readonly Type[] _skippableExceptions;

    /// <summary>Creates a skip policy from explicit exception types and a limit.</summary>
    /// <param name="skippableExceptions">Exception types that are safe to skip.</param>
    /// <param name="skipLimit">Maximum number of items to skip before failing the step.</param>
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

    /// <summary>A policy that never skips.</summary>
    public static SkipPolicy None => new(skippableExceptions: [], skipLimit: 0);

    /// <summary>Creates a policy that skips up to <paramref name="maxSkips"/> items for the given exception type.</summary>
    public static SkipPolicy For<TException>(int maxSkips) where TException : Exception
        => new([typeof(TException)], maxSkips);

    /// <inheritdoc cref="For{TException}(int)" />
    public static SkipPolicy For<TException1, TException2>(int maxSkips)
        where TException1 : Exception
        where TException2 : Exception
        => new([typeof(TException1), typeof(TException2)], maxSkips);

    /// <inheritdoc cref="For{TException}(int)" />
    public static SkipPolicy For<TException1, TException2, TException3>(int maxSkips)
        where TException1 : Exception
        where TException2 : Exception
        where TException3 : Exception
        => new([typeof(TException1), typeof(TException2), typeof(TException3)], maxSkips);
}
