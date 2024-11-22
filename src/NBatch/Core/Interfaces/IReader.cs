namespace NBatch.Core.Interfaces;

public interface IReader<TItem>
{
    Task<IEnumerable<TItem>> ReadAsync(long startIndex, int chunkSize);
}
