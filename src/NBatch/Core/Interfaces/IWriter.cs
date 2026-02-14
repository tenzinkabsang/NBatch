namespace NBatch.Core.Interfaces;

public interface IWriter<TItem>
{
    Task WriteAsync(IEnumerable<TItem> items, CancellationToken cancellationToken = default);
}
