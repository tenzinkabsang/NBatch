using System.Runtime.CompilerServices;

namespace NBatch.Readers.FileReader.Services;

internal sealed class FileService(string resourceUrl) : IFileService, IDisposable
{
    private StreamReader? _reader;
    private long _currentLine;

    public async IAsyncEnumerable<string> ReadLinesAsync(long startIndex, int chunkSize, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // First call:       _reader is null → open the file.
        // Backward seek:    startIndex < _currentLine → reset and reopen.
        // Sequential chunk: _reader is open and _currentLine == startIndex → skip this block entirely.
        if (_reader is null || startIndex < _currentLine)
        {
            Reset();
            _reader = File.OpenText(resourceUrl);
            _currentLine = 0;
        }

        // For sequential chunks, _currentLine already equals startIndex so this loop is a no-op.
        // For forward skips (e.g., gap between header read and first data chunk), this advances
        // the reader from its current position — not from line 0.
        while (_currentLine < startIndex)
        {
            if (await _reader.ReadLineAsync(cancellationToken) is null)
                yield break;

            _currentLine++;
        }

        // Yield up to chunkSize lines from the current position.
        for (int i = 0; i < chunkSize; i++)
        {
            var line = await _reader.ReadLineAsync(cancellationToken);
            if (line is null)
                yield break;

            _currentLine++;
            yield return line;
        }
    }

    private void Reset()
    {
        _reader?.Dispose();
        _reader = null;
        _currentLine = 0;
    }

    public void Dispose() => Reset();
}
