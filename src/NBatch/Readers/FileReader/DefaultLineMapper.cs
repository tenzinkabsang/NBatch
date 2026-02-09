namespace NBatch.Readers.FileReader;

internal sealed class DefaultLineMapper<T> : ILineMapper<T>
{
    private readonly IFieldSetMapper<T> _mapper;

    public ILineTokenizer Tokenizer { get; init; }

    public DefaultLineMapper(ILineTokenizer tokenizer, IFieldSetMapper<T> mapper)
    {
        _mapper = mapper;
        Tokenizer = tokenizer;
    }

    public T MapToModel(string line)
    {
        FieldSet fieldSet = Tokenizer.Tokenize(line);
        return _mapper.MapFieldSet(fieldSet);
    }
}
