using NBatch.Core.Interfaces;

namespace NBatch.Core;

internal sealed class DelegateWriter<TItem>(Func<IEnumerable<TItem>, Task> writeAction) : IWriter<TItem>
{
    public Task WriteAsync(IEnumerable<TItem> items, CancellationToken cancellationToken = default) => writeAction(items);
}
