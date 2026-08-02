namespace NBatch.Readers.FileReader;

/// <summary>
/// Thrown when a line in a flat file cannot be parsed into the target type.
/// File I/O errors (e.g. a missing file) are <em>not</em> wrapped in this type —
/// they propagate as their original exception.
/// </summary>
public sealed class FlatFileParseException : Exception
{
    /// <summary>The 1-based physical line number in the source file, or null when unknown.</summary>
    public long? LineNumber { get; }

    /// <summary>Initializes a new instance with a default message.</summary>
    public FlatFileParseException() : base("Unable to parse file") { }

    /// <summary>Initializes a new instance wrapping an inner exception.</summary>
    /// <param name="innerException">The exception that caused the parse failure.</param>
    public FlatFileParseException(Exception innerException)
        : base("Unable to parse file", innerException) { }

    /// <summary>Initializes a new instance for a specific line, wrapping an inner exception.</summary>
    /// <param name="lineNumber">The 1-based physical line number that failed to parse.</param>
    /// <param name="innerException">The exception that caused the parse failure.</param>
    public FlatFileParseException(long lineNumber, Exception innerException)
        : base($"Unable to parse line {lineNumber}.", innerException)
        => LineNumber = lineNumber;
}
