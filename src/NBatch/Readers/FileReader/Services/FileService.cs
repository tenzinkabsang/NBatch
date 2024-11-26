namespace NBatch.Readers.FileReader.Services;

internal sealed class FileService(string resourceUrl) : IFileService
{
    public async IAsyncEnumerable<string> ReadLinesAsync(long startIndex, int chunkSize)
    {
        int rowCounter = -1;
        int chunkCounter = 0;
        using StreamReader reader = File.OpenText(resourceUrl);
        string? input;
        while ((input = await reader.ReadLineAsync()) != null)
        {
            /** If the current row is less than the start index, then skip **/
            if (RowAlreadyProcessed(startIndex, ref rowCounter))
                continue;

            /** If the specified chunk size is reached than we are done. **/
            if (HasReachedChunkSize(chunkSize, ref chunkCounter))
                break;

            yield return input;
        }
    }

    /// <summary>
    /// Returns true if the current row is less than the startIndex
    /// </summary>
    private static bool RowAlreadyProcessed(long startIndex, ref int rowCounter)
        => ++rowCounter < startIndex;

    /// <summary>
    /// Returns true if the chunk size has been reached.
    /// </summary>
    private static bool HasReachedChunkSize(int chunkSize, ref int chunkCounter)
        => ++chunkCounter > chunkSize;

}
