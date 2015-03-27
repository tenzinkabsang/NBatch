namespace NBatch.Main.Readers.FileReaders
{
    public interface ILineTokenizer
    {
        FieldSet Tokenize(string line);
        string[] Headers { get; }
    }
}