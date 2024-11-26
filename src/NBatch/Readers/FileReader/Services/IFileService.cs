namespace NBatch.Readers.FileReader.Services;

internal interface IFileService
{
    IAsyncEnumerable<string> ReadLinesAsync(long startIndex, int chunkSize);
}
