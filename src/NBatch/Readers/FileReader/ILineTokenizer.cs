namespace NBatch.Readers.FileReader;

internal interface ILineTokenizer
{
    FieldSet Tokenize(string line);
    string[] Headers { get; }
}
