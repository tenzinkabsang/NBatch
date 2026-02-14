using NBatch.Core.Interfaces;

namespace NBatch.ConsoleApp;

public sealed class ConsoleWriter<TItem> : IWriter<TItem>
{
    public Task WriteAsync(IEnumerable<TItem> items, CancellationToken cancellationToken = default)
    {
        foreach (var item in items)
            Console.WriteLine(item);

        return Task.CompletedTask;
    }
}
