using System.Runtime.CompilerServices;

namespace NBatch.Readers.FileReader.Services;

internal sealed class FileService(string resourceUrl) : IFileService, IDisposable
{
    private StreamReader? _reader;
    private long _position;      // non-blank (data) lines consumed — the position index
    private long _physicalLine;  // all lines consumed — 1-based numbering for yielded lines

    public async IAsyncEnumerable<CsvLine> ReadLinesAsync(long startIndex, int chunkSize, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // First call:       _reader is null → open the file.
        // Backward seek:    startIndex < _position → reset and reopen.
        // Sequential chunk: _reader is open and _position == startIndex → skip loop is a no-op.
        if (_reader is null || startIndex < _position)
        {
            Reset();
            _reader = File.OpenText(resourceUrl);
        }

        // Skip forward to startIndex, counting only non-blank lines: blank lines
        // never occupy positions, so a blank stretch of the file can never make a
        // chunk read as end-of-data.
        while (_position < startIndex)
        {
            var line = await _reader.ReadLineAsync(cancellationToken);
            if (line is null)
                yield break;

            _physicalLine++;
            if (!string.IsNullOrWhiteSpace(line))
                _position++;
        }

        // Yield up to chunkSize non-blank lines from the current position.
        int yielded = 0;
        while (yielded < chunkSize)
        {
            var line = await _reader.ReadLineAsync(cancellationToken);
            if (line is null)
                yield break;

            _physicalLine++;
            if (string.IsNullOrWhiteSpace(line))
                continue;

            _position++;
            yielded++;
            yield return new CsvLine(_physicalLine, line);
        }
    }

    private void Reset()
    {
        _reader?.Dispose();
        _reader = null;
        _position = 0;
        _physicalLine = 0;
    }

    public void Dispose() => Reset();
}
