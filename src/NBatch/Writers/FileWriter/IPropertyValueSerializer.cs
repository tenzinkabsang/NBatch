namespace NBatch.Writers.FileWriter;

internal interface IPropertyValueSerializer
{
    IEnumerable<string> Serialize<T>(IEnumerable<T> items) where T : class;
    char Token { get; set; }
}
