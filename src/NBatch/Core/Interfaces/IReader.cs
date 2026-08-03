namespace NBatch.Core.Interfaces;

/// <summary>Reads items in paginated chunks.</summary>
/// <typeparam name="TItem">The type of items to read.</typeparam>
public interface IReader<TItem>
{
    /// <summary>
    /// Reads a chunk of items starting at the given position.
    /// <para>
    /// <b>Positional contract</b> — the step engine tracks progress (and resumes
    /// after a failure) by item position, so implementations must guarantee:
    /// </para>
    /// <list type="bullet">
    /// <item><description><b>Stable positions:</b> the same <paramref name="startIndex"/>
    /// must map to the same item on every call — including across process restarts.
    /// For database readers this requires a deterministic ORDER BY.</description></item>
    /// <item><description><b>Full chunks:</b> return exactly <paramref name="chunkSize"/>
    /// items for every range before the end of the data. A shorter result is only valid
    /// for the final chunk; an empty result means end of data. The engine advances by
    /// <paramref name="chunkSize"/> positions per chunk and fails the step if more items
    /// appear after a partial chunk.</description></item>
    /// </list>
    /// </summary>
    /// <param name="startIndex">Zero-based position of the first item to read.</param>
    /// <param name="chunkSize">Maximum number of items to return.</param>
    /// <param name="cancellationToken">Token to cancel the read.</param>
    Task<IEnumerable<TItem>> ReadAsync(long startIndex, int chunkSize, CancellationToken cancellationToken = default);
}
