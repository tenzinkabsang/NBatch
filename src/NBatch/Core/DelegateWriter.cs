using NBatch.Core.Interfaces;

namespace NBatch.Core;

internal sealed class DelegateWriter<TItem> : IWriter<TItem>
{
    private readonly Func<IEnumerable<TItem>, CancellationToken, Task> _writeAction;

    internal DelegateWriter(Func<IEnumerable<TItem>, Task> writeAction)
    {
        _writeAction = (items, _) => writeAction(items);
    }

    internal DelegateWriter(Func<IEnumerable<TItem>, CancellationToken, Task> writeAction)
    {
        _writeAction = writeAction;
    }

    public Task WriteAsync(IEnumerable<TItem> items, CancellationToken cancellationToken = default) => _writeAction(items, cancellationToken);
}
