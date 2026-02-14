using System.Runtime.CompilerServices;

namespace NBatch.Readers.FileReader.Services;

internal sealed class FileService(string resourceUrl) : IFileService
{
    public async IAsyncEnumerable<string> ReadLinesAsync(long startIndex, int chunkSize, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        int rowCounter = -1;
        int chunkCounter = 0;
        using StreamReader reader = File.OpenText(resourceUrl);
        string? input;
        while ((input = await reader.ReadLineAsync(cancellationToken)) != null)
        {
            // If the current row is less than the start index, then skip
            if (RowAlreadyProcessed(startIndex, ref rowCounter))
                continue;

            // If the specified chunk size is reached then we are done
            if (HasReachedChunkSize(chunkSize, ref chunkCounter))
                break;

            yield return input;
        }
    }

    /// <summary>
    /// Determines whether the current row has already been processed based on the specified starting index.
    /// </summary>
    private static bool RowAlreadyProcessed(long startIndex, ref int rowCounter)
        => ++rowCounter < startIndex;

    /// <summary>
    /// Determines whether the current chunk counter has exceeded the specified chunk size.
    /// </summary>
    private static bool HasReachedChunkSize(int chunkSize, ref int chunkCounter)
        => ++chunkCounter > chunkSize;

}
