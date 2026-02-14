namespace NBatch.Core.Interfaces;

/// <summary>Reads items in paginated chunks.</summary>
/// <typeparam name="TItem">The type of items to read.</typeparam>
public interface IReader<TItem>
{
    /// <summary>Reads a chunk of items starting at the given index.</summary>
    Task<IEnumerable<TItem>> ReadAsync(long startIndex, int chunkSize, CancellationToken cancellationToken = default);
}
