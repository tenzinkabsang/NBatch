namespace NBatch.Main.Readers.FileReaders
{
    public interface ILineMapper<out T>
    {
        ILineTokenizer Tokenizer { get; }
        T MapToModel(string line);
    }
}