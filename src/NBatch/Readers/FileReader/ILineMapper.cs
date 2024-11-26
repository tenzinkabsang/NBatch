namespace NBatch.Readers.FileReader;

internal interface ILineMapper<T>
{
    ILineTokenizer Tokenizer { get; }
    T MapToModel(string line);
}
