namespace NBatch.Core.Interfaces;

public interface IProcessor<TInput, TOutput>
{
    Task<TOutput> ProcessAsync(TInput input);
}
