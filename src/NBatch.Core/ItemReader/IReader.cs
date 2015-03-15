using System.Collections.Generic;

namespace NBatch.Core.ItemReader
{
    public interface IReader<out TInput>
    {
        int LinesToSkip { get; set; }
        IEnumerable<TInput> Read(int startIndex, int chunkSize);
    }
}