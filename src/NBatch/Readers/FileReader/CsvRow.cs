namespace NBatch.Readers.FileReader;

/// <summary>
/// Represents a single parsed row from a delimited file.
/// Provides typed accessors to retrieve column values by header name or index.
/// </summary>
public sealed class CsvRow
{
    private readonly Dictionary<string, string> _values;

    private CsvRow(Dictionary<string, string> values) => _values = values;

    internal static CsvRow Create(IList<string> headers, IList<string> columns)
    {
        var keys = headers.Count > 0 ? headers : DefaultKeys(columns);

        var result = keys
            .Zip(columns, (k, v) => (Key: k, Value: v.Trim()))
            .ToDictionary(x => x.Key, x => x.Value);

        return new CsvRow(result);
    }

    private static IEnumerable<string> DefaultKeys(IList<string> columns)
        => Enumerable.Range(0, columns.Count).Select(i => i.ToString());

    public string GetString(string column) => GetValue(column);
    public string GetString(int index) => GetValue(index.ToString());

    public int GetInt(string column) => int.Parse(GetValue(column));
    public int GetInt(int index) => int.Parse(GetValue(index.ToString()));

    public decimal GetDecimal(string column) => decimal.Parse(GetValue(column));
    public decimal GetDecimal(int index) => decimal.Parse(GetValue(index.ToString()));

    public long GetLong(string column) => long.Parse(GetValue(column));
    public long GetLong(int index) => long.Parse(GetValue(index.ToString()));

    public double GetDouble(string column) => double.Parse(GetValue(column));
    public double GetDouble(int index) => double.Parse(GetValue(index.ToString()));

    public bool GetBool(string column) => bool.Parse(GetValue(column));
    public bool GetBool(int index) => bool.Parse(GetValue(index.ToString()));

    private string GetValue(string key)
    {
        if (!_values.TryGetValue(key, out var value))
            throw new KeyNotFoundException($"No column with the key '{key}' exists.");
        return value;
    }
}
