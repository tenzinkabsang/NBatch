namespace NBatch.Readers.FileReader;

public sealed class FlatFileParseException : Exception
{
    public FlatFileParseException() : base("Unable to parse file") { }

    public FlatFileParseException(Exception innerException)
        : base("Unable to parse file", innerException) { }
}
