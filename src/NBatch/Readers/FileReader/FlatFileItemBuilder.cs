using NBatch.Readers.FileReader.Services;

namespace NBatch.Readers.FileReader;

public sealed class FlatFileItemBuilder<TItem>(string resourceUrl, IFieldSetMapper<TItem> fieldMapper)
{
    private string[]? _headers;
    private int _linesToSkip;
    private char? _token;

    public FlatFileItemBuilder<TItem> WithHeaders(params string[] headers)
    {
        _headers = headers;
        return this;
    }

    public FlatFileItemBuilder<TItem> WithLinesToSkip(int linesToSkip)
    {
        _linesToSkip = linesToSkip;
        return this;
    }

    public FlatFileItemBuilder<TItem> WithToken(char token)
    {
        _token = token;
        return this;
    }

    public FlatFileItemReader<TItem> Build()
    {
        var lineTokenizer = new DelimitedLineTokenizer(_headers, _token);
        var lineMapper = new DefaultLineMapper<TItem>(lineTokenizer, fieldMapper);
        return new FlatFileItemReader<TItem>(lineMapper, new FileService(resourceUrl)) { LinesToSkip = _linesToSkip };
    }
}
