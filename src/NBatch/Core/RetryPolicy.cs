namespace NBatch.Core;

/// <summary>
/// Configure transient exceptions that are safe to retry. Retries happen
/// <em>before</em> the <see cref="SkipPolicy"/> is consulted, so a transient
/// error that succeeds on retry consumes no skip budget.
/// <para>
/// Matching follows the same rules as <see cref="SkipPolicy"/>: subclasses match,
/// and the inner-exception chain is walked.
/// </para>
/// </summary>
public sealed class RetryPolicy
{
    private readonly Type[] _retryableExceptions;
    private readonly TimeSpan _delay;
    private readonly double _backoffMultiplier;

    /// <summary>Creates a retry policy from explicit exception types.</summary>
    /// <param name="retryableExceptions">Exception types that are safe to retry.</param>
    /// <param name="maxAttempts">Total attempts including the first (minimum 1; 1 means no retries).</param>
    /// <param name="delay">Wait between attempts. <see cref="TimeSpan.Zero"/> retries immediately.</param>
    public RetryPolicy(Type[] retryableExceptions, int maxAttempts, TimeSpan delay)
        : this(retryableExceptions, maxAttempts, delay, backoffMultiplier: 1.0)
    {
    }

    private RetryPolicy(Type[] retryableExceptions, int maxAttempts, TimeSpan delay, double backoffMultiplier)
    {
        ArgumentNullException.ThrowIfNull(retryableExceptions);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxAttempts, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(delay, TimeSpan.Zero);

        if (!Array.TrueForAll(retryableExceptions, IsExceptionType))
            throw new ArgumentException("All types must derive from Exception.", nameof(retryableExceptions));

        _retryableExceptions = retryableExceptions;
        MaxAttempts = maxAttempts;
        _delay = delay;
        _backoffMultiplier = backoffMultiplier;
    }

    internal int MaxAttempts { get; }

    /// <summary>
    /// Returns a copy of this policy whose delay grows by <paramref name="multiplier"/>
    /// after each failed attempt (exponential backoff).
    /// </summary>
    /// <param name="multiplier">Growth factor per attempt (minimum 1.0).</param>
    public RetryPolicy WithBackoffMultiplier(double multiplier)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(multiplier, 1.0);
        return new RetryPolicy(_retryableExceptions, MaxAttempts, _delay, multiplier);
    }

    /// <summary>Whether the exception is of a retryable type.</summary>
    internal bool Matches(Exception exception)
        => _retryableExceptions.Length > 0
           && MaxAttempts > 1
           && ExceptionTypeMatcher.Matches(_retryableExceptions, exception);

    /// <summary>The wait before the next attempt, given how many attempts have already failed.</summary>
    internal TimeSpan GetDelay(int completedAttempts)
        => _delay == TimeSpan.Zero
            ? TimeSpan.Zero
            : _delay * Math.Pow(_backoffMultiplier, completedAttempts - 1);

    private static bool IsExceptionType(Type type)
        => type == typeof(Exception) || type.IsSubclassOf(typeof(Exception));

    /// <summary>A policy that never retries.</summary>
    public static RetryPolicy None { get; } = new(retryableExceptions: [], maxAttempts: 1, TimeSpan.Zero);

    /// <summary>Creates a policy that retries the given exception type.</summary>
    /// <param name="maxAttempts">Total attempts including the first.</param>
    /// <param name="delay">Wait between attempts; omit to retry immediately.</param>
    public static RetryPolicy For<TException>(int maxAttempts, TimeSpan? delay = null)
        where TException : Exception
        => new([typeof(TException)], maxAttempts, delay ?? TimeSpan.Zero);

    /// <inheritdoc cref="For{TException}(int, TimeSpan?)" />
    public static RetryPolicy For<TException1, TException2>(int maxAttempts, TimeSpan? delay = null)
        where TException1 : Exception
        where TException2 : Exception
        => new([typeof(TException1), typeof(TException2)], maxAttempts, delay ?? TimeSpan.Zero);

    /// <inheritdoc cref="For{TException}(int, TimeSpan?)" />
    public static RetryPolicy For<TException1, TException2, TException3>(int maxAttempts, TimeSpan? delay = null)
        where TException1 : Exception
        where TException2 : Exception
        where TException3 : Exception
        => new([typeof(TException1), typeof(TException2), typeof(TException3)], maxAttempts, delay ?? TimeSpan.Zero);
}
