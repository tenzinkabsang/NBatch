using System.Globalization;

namespace NBatch.Writers.FileWriter;

/// <summary>
/// Serializes each item with the given separator. Values are formatted with the
/// invariant culture, and fields containing the delimiter, a quote, or a line
/// break are quoted RFC 4180-style (quotes escaped by doubling) so the output
/// round-trips through <c>CsvReader</c> and other CSV consumers.
/// </summary>
internal sealed class PropertyValueSerializer(char token = ',') : IPropertyValueSerializer
{
    public char Token { get; set; } = token;

    public IEnumerable<string> Serialize<T>(IEnumerable<T> items) where T : class
    {
        if (items is null)
            return [];

        var materializedItems = items as IList<T> ?? items.ToList();

        if (materializedItems.Count == 0)
            return [];

        var props = typeof(T).GetProperties();

        return materializedItems.Select(item =>
            string.Join(Token, props.Select(p => Escape(Convert.ToString(p.GetValue(item), CultureInfo.InvariantCulture)))));
    }

    private string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        bool needsQuoting = value.Contains(Token) || value.Contains('"') || value.Contains('\r') || value.Contains('\n');
        return needsQuoting ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
    }
}
