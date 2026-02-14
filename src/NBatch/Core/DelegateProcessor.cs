using NBatch.Core.Interfaces;

namespace NBatch.Core;

internal sealed class DelegateProcessor<TInput, TOutput>(Func<TInput, TOutput> transform) : IProcessor<TInput, TOutput>
{
    public Task<TOutput> ProcessAsync(TInput input, CancellationToken cancellationToken = default) => Task.FromResult(transform(input));
}
