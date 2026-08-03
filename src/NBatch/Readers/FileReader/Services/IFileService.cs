namespace NBatch.Readers.FileReader.Services;

/// <summary>
/// A single non-blank line of a delimited file, tagged with its 1-based physical
/// line number so parse errors can point at the exact line.
/// </summary>
internal readonly record struct CsvLine(long PhysicalLineNumber, string Text);

internal interface IFileService
{
    /// <summary>
    /// Reads up to <paramref name="chunkSize"/> non-blank lines starting at
    /// data-line position <paramref name="startIndex"/>. Blank (whitespace-only)
    /// lines never occupy positions — an empty result therefore always means end
    /// of data, never a blank stretch of the file.
    /// </summary>
    IAsyncEnumerable<CsvLine> ReadLinesAsync(long startIndex, int chunkSize, CancellationToken cancellationToken = default);
}
