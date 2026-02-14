namespace NBatch.Core;

/// <summary>
/// Configure exceptions that are safe to retry. When a retryable exception is thrown,
/// the chunk is re-executed up to <c>maxAttempts</c> times (including the initial attempt)
/// before falling through to the <see cref="SkipPolicy"/>.
/// </summary>
public sealed class RetryPolicy
{
    private readonly int _maxAttempts;
    private readonly TimeSpan _delay;
    private readonly Type[] _retryableExceptions;

    public RetryPolicy(Type[] retryableExceptions, int maxAttempts, TimeSpan delay = default)
    {
        ArgumentNullException.ThrowIfNull(retryableExceptions);

        if (!retryableExceptions.All(IsExceptionType))
            throw new ArgumentException("All types must derive from Exception.", nameof(retryableExceptions));

        _retryableExceptions = retryableExceptions;
        _maxAttempts = maxAttempts;
        _delay = delay;
    }

    /// <summary>
    /// Returns true if the exception is retryable and the attempt count has not been exhausted.
    /// <paramref name="attempt"/> is 1-based (1 = the initial attempt just failed).
    /// </summary>
    internal bool ShouldRetry(Exception exception, int attempt)
    {
        if (_retryableExceptions.Length == 0 || _maxAttempts <= 1)
            return false;

        return attempt < _maxAttempts && _retryableExceptions.Contains(exception.GetType());
    }

    internal async Task WaitAsync(CancellationToken cancellationToken = default)
    {
        if (_delay > TimeSpan.Zero)
            await Task.Delay(_delay, cancellationToken);
    }

    private static bool IsExceptionType(Type type)
        => type == typeof(Exception) || type.IsSubclassOf(typeof(Exception));

    public static RetryPolicy None => new(retryableExceptions: [], maxAttempts: 0);
}
