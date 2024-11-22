namespace NBatch.Core.Interfaces;

public interface IWriter<TItem>
{
    Task<bool> WriteAsync(IEnumerable<TItem> items);
}
