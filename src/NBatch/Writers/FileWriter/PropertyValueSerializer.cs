namespace NBatch.Writers.FileWriter;

/// <summary>
/// Serializes each item with the given separator.
/// </summary>
internal sealed class PropertyValueSerializer(char token = ',') : IPropertyValueSerializer
{
    public char Token { get; set; } = token;

    public IEnumerable<string> Serialize<T>(IEnumerable<T> items) where T : class
    {
        if (items is null)
            return [];

        var props = items.First().GetType().GetProperties();

        return items.Select(item =>
            string.Join(Token, props.Select(p => p.GetValue(item))));
    }
}
