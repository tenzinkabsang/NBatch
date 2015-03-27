using System.Collections.Generic;

namespace NBatch.Main.Readers.FileReaders.Services
{
    public interface IFileService
    {
        IEnumerable<string> ReadLines(long startIndex, int chunkSize);
    }
}