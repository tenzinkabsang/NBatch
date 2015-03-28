namespace NBatch.Main.Readers.FileReader
{
    public interface ILineMapper<out T>
    {
        ILineTokenizer Tokenizer { get; }
        T MapToModel(string line);
    }
}