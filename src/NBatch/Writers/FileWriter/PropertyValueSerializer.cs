namespace NBatch.Writers.FileWriter;

/// <summary>
/// Serializes each item with the given separator.
/// </summary>
/// <param name="token">Separator token.</param>
internal sealed class PropertyValueSerializer(char token = ',') : IPropertyValueSerializer
{
    public char Token { get; set; } = token;

    public IEnumerable<string> Serialize<T>(IEnumerable<T> items) where T : class
    {
        if (items is null)
            return [];

        // Get all public properties for item.
        var props = items.First().GetType().GetProperties();

        return items.Select(item =>
        {
            // Get values for each property, adding tokens in between, then remove the initial token from the front.
            return props.Aggregate(string.Empty, (s, propInfo) => $"{s}{Token}{propInfo.GetValue(item)}").Substring(1);
        });
    }
}
