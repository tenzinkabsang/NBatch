using System.Collections.Generic;

namespace NBatch.Core.Reader.FileReader.Services
{
    public interface IFileService
    {
        IEnumerable<string> ReadLines(int startIndex, int chunkSize);
    }
}