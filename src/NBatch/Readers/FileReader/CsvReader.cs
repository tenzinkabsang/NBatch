using NBatch.Core.Interfaces;
using NBatch.Readers.FileReader.Services;

namespace NBatch.Readers.FileReader;

/// <summary>
/// Reads items from a delimited text file (CSV, TSV, pipe-delimited, etc.).
/// Automatically reads header names from the first row of the file.
/// <para>
/// Usage:
/// <code>
/// new CsvReader&lt;Product&gt;("products.csv", row => new Product
/// {
///     Name = row.GetString("Name"),
///     Price = row.GetDecimal("Price")
/// })
/// </code>
/// </para>
/// </summary>
public sealed class CsvReader<T> : IReader<T>
{
    private readonly IFileService _fileService;
    private readonly Func<CsvRow, T> _map;
    private char _delimiter = ',';
    private string[]? _headers;
    private bool _headersResolved;

    public CsvReader(string filePath, Func<CsvRow, T> map)
        : this(filePath, map, new FileService(filePath)) { }

    internal CsvReader(string filePath, Func<CsvRow, T> map, IFileService fileService)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        ArgumentNullException.ThrowIfNull(map);
        _map = map;
        _fileService = fileService;
    }

    /// <summary>
    /// Override the default comma delimiter.
    /// </summary>
    public CsvReader<T> WithDelimiter(char delimiter)
    {
        _delimiter = delimiter;
        return this;
    }

    /// <summary>
    /// Explicitly set column headers instead of reading them from the first row.
    /// When set, no lines are auto-skipped for headers.
    /// </summary>
    public CsvReader<T> WithHeaders(params string[] headers)
    {
        _headers = headers;
        _headersResolved = true;
        return this;
    }

    public async Task<IEnumerable<T>> ReadAsync(long startIndex, int chunkSize, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_headersResolved)
            {
                var headerLine = await _fileService.ReadLinesAsync(0, 1, cancellationToken)
                    .FirstOrDefaultAsync(cancellationToken);

                _headers = headerLine?.Split(_delimiter).Select(h => h.Trim()).ToArray() ?? [];
                _headersResolved = true;
            }

            // When headers come from the first row, offset by 1 to skip the header line
            long adjustedIndex = startIndex + 1;

            var lines = await _fileService.ReadLinesAsync(adjustedIndex, chunkSize, cancellationToken)
                .ToListAsync(cancellationToken);

            return lines
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Select(line =>
                {
                    var columns = line.Split(_delimiter);
                    var row = CsvRow.Create(_headers!, columns);
                    return _map(row);
                });
        }
        catch (Exception ex) when (ex is not FlatFileParseException)
        {
            throw new FlatFileParseException(ex);
        }
    }
}
