using System.Collections.Generic;

namespace NBatch.Main.Core;

public interface IReader<TItem>
{
    //int LinesToSkip { get; set; }
    IEnumerable<TItem> Read(long startIndex, int chunkSize);
}