namespace NBatch.Core.Reader.FileReader
{
    public interface ILineTokenizer
    {
        FieldSet Tokenize(string line);
        string[] Headers { get; }
    }
}