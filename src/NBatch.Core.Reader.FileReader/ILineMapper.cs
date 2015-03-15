namespace NBatch.Core.Reader.FileReader
{
    public interface ILineMapper<out T>
    {
        ILineTokenizer Tokenizer { get; }
        T MapToModel(string line);
    }
}