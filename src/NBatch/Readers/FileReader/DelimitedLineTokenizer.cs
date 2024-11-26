namespace NBatch.Readers.FileReader;

/// <summary>
/// Tokenizes each row using the provided separator token and header values.
/// </summary>
internal sealed class DelimitedLineTokenizer : ILineTokenizer
{
    public const char DEFAULT_TOKEN = ',';

    private readonly char _token;

    public string[] Headers { get; init; }

    public DelimitedLineTokenizer(char token = DEFAULT_TOKEN)
        : this([], token)
    {
    }

    public DelimitedLineTokenizer(string[]? headers, char? token)
    {
        Headers = headers ?? [];
        _token = token ?? DEFAULT_TOKEN;
    }

    /// <summary>
    /// Tokenize the line based on the separator token and header values.
    /// </summary>
    /// <param name="line">The current line being read from file.</param>
    /// <returns>FieldSet</returns>
    public FieldSet Tokenize(string line)
    {
        string[] rowItems = line.Split(_token);
        return FieldSet.Create(Headers, rowItems);
    }
}
