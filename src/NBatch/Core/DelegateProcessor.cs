using NBatch.Core.Interfaces;

namespace NBatch.Core;

internal sealed class DelegateProcessor<TInput, TOutput> : IProcessor<TInput, TOutput>
{
    private readonly Func<TInput, CancellationToken, Task<TOutput>> _transform;

    internal DelegateProcessor(Func<TInput, TOutput> transform)
    {
        _transform = (input, _) => Task.FromResult(transform(input));
    }

    internal DelegateProcessor(Func<TInput, CancellationToken, Task<TOutput>> transform)
    {
        _transform = transform;
    }

    public Task<TOutput> ProcessAsync(TInput input, CancellationToken cancellationToken = default) => _transform(input, cancellationToken);
}
