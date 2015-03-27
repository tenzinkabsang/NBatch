using NBatch.Main.Readers.FileReaders.Services;

namespace NBatch.Main.Readers.FileReaders
{
    public class FlatFileItemBuilder<TItem>
    {
        private readonly string _resourceUrl;
        private readonly IFieldSetMapper<TItem> _fieldMapper;
        private string[] _headers;
        private int _linesToSkip;
        private char? _token;

        public FlatFileItemBuilder(string resourceUrl, IFieldSetMapper<TItem> fieldMapper)
        {
            _resourceUrl = resourceUrl;
            _fieldMapper = fieldMapper;
        }

        public FlatFileItemBuilder<TItem> WithHeaders(string[] headers)
        {
            _headers = headers;
            return this;
        }

        public FlatFileItemBuilder<TItem> LinesToSkip(int linesToSkip)
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
            var lineMapper = new DefaultLineMapper<TItem>(lineTokenizer, _fieldMapper);
            var itemReader = new FlatFileItemReader<TItem>(lineMapper, new FileService(_resourceUrl))
                             {
                                 LinesToSkip = _linesToSkip
                             };
            return itemReader;
        }
    }
}