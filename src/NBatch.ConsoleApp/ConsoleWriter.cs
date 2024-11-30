using NBatch.Core.Interfaces;

namespace NBatch.ConsoleApp;

public sealed class ConsoleWriter<TItem> : IWriter<TItem>
{
    public async Task<bool> WriteAsync(IEnumerable<TItem> items)
    {
        var result = await Task.FromResult(true);
        foreach (var item in items)
            Console.WriteLine(item);

        return result;
    }
}
