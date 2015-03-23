using System.Collections.Generic;

namespace NBatch.Core.ItemReader
{
    public interface IReader<out TInput>
    {
        int LinesToSkip { get; set; }
        IEnumerable<TInput> Read(long startIndex, int chunkSize);
    }
}