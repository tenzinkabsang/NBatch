using NBatch.Core.Interfaces;
using NBatch.Readers.FileReader.Services;

namespace NBatch.Readers.FileReader;

/// <summary>
/// Reads items from a delimited text file (CSV, TSV, pipe-delimited, etc.).
/// Automatically reads header names from the first row of the file.
/// Fields may be quoted (RFC 4180): quoted fields can contain the delimiter, and
/// <c>""</c> escapes a literal quote. Embedded newlines are not supported.
/// <para>
/// Per-line parse and mapping errors are wrapped in <see cref="FlatFileParseException"/>
/// carrying the failing line number; file I/O errors (e.g. a missing file)
/// propagate unwrapped.
/// </para>
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
public sealed class CsvReader<T> : IReader<T>, IDisposable
{
    private readonly string _filePath;
    private readonly IFileService _fileService;
    private readonly Func<CsvRow, T> _map;
    private char _delimiter = ',';
    private string[]? _headers;
    private bool _headersResolved;
    private bool _headersFromFile;

    /// <summary>
    /// Creates a reader that automatically maps header names to public settable
    /// properties of <typeparamref name="T"/> (case-insensitive, invariant culture).
    /// Requires a public parameterless constructor; positional records need the
    /// map-function overload.
    /// </summary>
    /// <param name="filePath">Path to the delimited file.</param>
    /// <exception cref="InvalidOperationException"><typeparamref name="T"/> cannot be auto-mapped.</exception>
    public CsvReader(string filePath)
        : this(filePath, CsvAutoMapper<T>.CreateMap(), new FileService(filePath)) { }

    /// <summary>Creates a reader for the specified file with a row-mapping function.</summary>
    /// <param name="filePath">Path to the delimited file.</param>
    /// <param name="map">A function that maps each <see cref="CsvRow"/> to <typeparamref name="T"/>.</param>
    public CsvReader(string filePath, Func<CsvRow, T> map)
        : this(filePath, map, new FileService(filePath)) { }

    internal CsvReader(string filePath, IFileService fileService)
        : this(filePath, CsvAutoMapper<T>.CreateMap(), fileService) { }

    internal CsvReader(string filePath, Func<CsvRow, T> map, IFileService fileService)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        ArgumentNullException.ThrowIfNull(map);
        _filePath = filePath;
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
    /// <exception cref="ArgumentException">A header name appears more than once.</exception>
    public CsvReader<T> WithHeaders(params string[] headers)
    {
        ArgumentNullException.ThrowIfNull(headers);
        var duplicate = headers.GroupBy(h => h).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
            throw new ArgumentException($"Duplicate column header '{duplicate.Key}'.", nameof(headers));

        _headers = headers;
        _headersResolved = true;
        return this;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<T>> ReadAsync(long startIndex, int chunkSize, CancellationToken cancellationToken = default)
    {
        if (!_headersResolved)
        {
            var headerLine = await _fileService.ReadLinesAsync(0, 1, cancellationToken)
                .FirstOrDefaultAsync(cancellationToken);
            ResolveHeaders(headerLine);
        }

        // When headers come from the first row, offset by 1 to skip the header line
        long adjustedIndex = _headersFromFile ? startIndex + 1 : startIndex;

        var lines = await _fileService.ReadLinesAsync(adjustedIndex, chunkSize, cancellationToken)
            .ToListAsync(cancellationToken);

        // Materialize eagerly so parse and mapping errors surface here, wrapped with
        // their line number — never lazily inside the step's enumeration.
        var results = new List<T>(lines.Count);
        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line))
                continue;

            long physicalLine = adjustedIndex + i + 1; // 1-based line number in the file
            try
            {
                var columns = CsvLineParser.Parse(line, _delimiter);
                var row = CsvRow.Create(_headers!, columns);
                results.Add(_map(row));
            }
            catch (Exception ex)
            {
                throw new FlatFileParseException(physicalLine, ex);
            }
        }

        return results;
    }

    private void ResolveHeaders(string? headerLine)
    {
        string[] headers;
        try
        {
            headers = headerLine is null
                ? []
                : CsvLineParser.Parse(headerLine, _delimiter).Select(h => h.Trim()).ToArray();
        }
        catch (Exception ex)
        {
            throw new FlatFileParseException(1, ex);
        }

        var duplicate = headers.Where(h => h.Length > 0).GroupBy(h => h).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
            throw new FlatFileParseException(1,
                new ArgumentException($"Duplicate column header '{duplicate.Key}' in '{_filePath}'."));

        _headers = headers;
        _headersResolved = true;
        _headersFromFile = true;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_fileService is IDisposable disposable)
            disposable.Dispose();
    }
}
