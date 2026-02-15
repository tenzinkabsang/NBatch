using System.Runtime.CompilerServices;

namespace NBatch.Readers.FileReader.Services;

internal sealed class FileService(string resourceUrl) : IFileService
{
    public async IAsyncEnumerable<string> ReadLinesAsync(long startIndex, int chunkSize, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using StreamReader reader = File.OpenText(resourceUrl);

        // Advance the reader to the desired starting line (skip lines 0 to startIndex-1).
        for (long i = 0; i < startIndex; i++)
        {
            // If we reach the end of the file before startIndex, exit early (nothing to yield).
            if (await reader.ReadLineAsync(cancellationToken) is null)
                yield break;
        }

        // Yield up to chunkSize lines, starting from line startIndex.
        for (int i = 0; i < chunkSize; i++)
        {
            // Read the next line; if end of file, stop yielding.
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
                yield break;

            yield return line;
        }
    }
}
