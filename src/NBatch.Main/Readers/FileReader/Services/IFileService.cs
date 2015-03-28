using System.Collections.Generic;

namespace NBatch.Main.Readers.FileReader.Services
{
    public interface IFileService
    {
        IEnumerable<string> ReadLines(long startIndex, int chunkSize);
    }
}