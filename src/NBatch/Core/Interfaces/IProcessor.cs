namespace NBatch.Core.Interfaces;

public interface IProcessor<T, U>
{
    Task<U> ProcessAsync(T input);
}
