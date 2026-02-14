namespace NBatch.Readers.FileReader;

/// <summary>
/// Thrown when a line in a flat file cannot be parsed into the target type.
/// </summary>
public sealed class FlatFileParseException : Exception
{
    /// <summary>Initializes a new instance with a default message.</summary>
    public FlatFileParseException() : base("Unable to parse file") { }

    /// <summary>Initializes a new instance wrapping an inner exception.</summary>
    /// <param name="innerException">The exception that caused the parse failure.</param>
    public FlatFileParseException(Exception innerException)
        : base("Unable to parse file", innerException) { }
}
