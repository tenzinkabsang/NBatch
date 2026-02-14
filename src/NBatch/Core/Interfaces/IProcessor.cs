namespace NBatch.Core.Interfaces;

/// <summary>Transforms an input item into an output item.</summary>
/// <typeparam name="TInput">The input type.</typeparam>
/// <typeparam name="TOutput">The output type.</typeparam>
public interface IProcessor<TInput, TOutput>
{
    /// <summary>Processes a single item.</summary>
    Task<TOutput> ProcessAsync(TInput input, CancellationToken cancellationToken = default);
}
