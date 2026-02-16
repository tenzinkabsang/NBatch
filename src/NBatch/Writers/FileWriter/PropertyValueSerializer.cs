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

        var materializedItems = items as IList<T> ?? items.ToList();

        if (materializedItems.Count == 0)
            return [];

        var props = typeof(T).GetProperties();

        return materializedItems.Select(item =>
            string.Join(Token, props.Select(p => p.GetValue(item))));
    }
}
