using System.Collections.Generic;

namespace NBatch.Core.ItemReader
{
    public interface IReader<out TItem>
    {
        int LinesToSkip { get; set; }
        IEnumerable<TItem> Read(long startIndex, int chunkSize);
    }
}