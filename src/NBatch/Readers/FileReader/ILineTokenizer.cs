namespace NBatch.Readers.FileReader;

public interface ILineTokenizer
{
    FieldSet Tokenize(string line);
    string[] Headers { get; }
}
