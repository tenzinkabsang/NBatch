namespace NBatch.Core.Interfaces;

/// <summary>Writes a batch of items to a destination.</summary>
/// <typeparam name="TItem">The type of items to write.</typeparam>
public interface IWriter<TItem>
{
    /// <summary>Writes the given items.</summary>
    Task WriteAsync(IEnumerable<TItem> items, CancellationToken cancellationToken = default);
}
