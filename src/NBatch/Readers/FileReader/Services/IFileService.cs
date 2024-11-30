namespace NBatch.Readers.FileReader.Services;

public interface IFileService
{
    IAsyncEnumerable<string> ReadLinesAsync(long startIndex, int chunkSize);
}
