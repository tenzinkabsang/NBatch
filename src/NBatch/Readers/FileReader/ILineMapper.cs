namespace NBatch.Readers.FileReader;

public interface ILineMapper<T>
{
    ILineTokenizer Tokenizer { get; }
    T MapToModel(string line);
}
