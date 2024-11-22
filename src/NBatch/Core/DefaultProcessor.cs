using NBatch.Core.Interfaces;

namespace NBatch.Core;

/// <summary>
/// Default processor that is used if no processor is provided. It simply returns the input item.
/// </summary>
/// <typeparam name="TInput">Input item to process.</typeparam>
/// <typeparam name="TOutput">Item returned after processing.</typeparam>
internal sealed class DefaultProcessor<TInput, TOutput> : IProcessor<TInput, TOutput>
{
    public Task<TOutput> ProcessAsync(TInput input) => Task.FromResult((dynamic)input);
}
