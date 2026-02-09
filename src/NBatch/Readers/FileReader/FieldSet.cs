namespace NBatch.Readers.FileReader;

public sealed class FieldSet
{
    /// <summary>
    /// Contains key value information for a single row e.g.,
    /// ["FirstName"] = "John"
    /// ["LastName"] = "Doe"
    /// ["Age"] = 20
    /// </summary>
    private readonly Dictionary<string, string> _valueWithHeader;

    private FieldSet(Dictionary<string, string> valueWithHeader) => _valueWithHeader = valueWithHeader;

    public static FieldSet Create(IList<string> headers, IList<string> line)
    {
        var keys = headers.Count > 0 ? headers : UseIndexAsKeys(line);

        var result = keys
            .Zip(line, (key, value) => (key, value))
            .ToDictionary(x => x.key, x => x.value);

        return new FieldSet(result);
    }

    /// <summary>
    /// If no header information is provided, use index as keys. 
    /// This allows the user to retrieve value based on index.
    /// </summary>
    private static IEnumerable<string> UseIndexAsKeys(IList<string> line)
        => Enumerable.Range(0, line.Count).Select(index => index.ToString());

    public string GetString(string key) => GetValue(key);

    public string GetString(int index) => GetValue(index.ToString());

    public decimal GetDecimal(int index) => GetDecimal(index.ToString());

    public decimal GetDecimal(string key) => decimal.Parse(GetValue(key));

    public int GetInt(int index) => GetInt(index.ToString());

    public int GetInt(string key) => int.Parse(GetValue(key));

    private string GetValue(string key)
    {
        if (!_valueWithHeader.TryGetValue(key, out var value))
            throw new KeyNotFoundException($"No value with the key '{key}' exists.");

        return value;
    }
}
