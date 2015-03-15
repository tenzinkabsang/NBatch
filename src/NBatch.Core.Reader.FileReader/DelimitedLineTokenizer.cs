namespace NBatch.Core.Reader.FileReader
{
    public class DelimitedLineTokenizer : ILineTokenizer
    {
        private readonly char _token;
        public const char DEFAULT_TOKEN = ',';

        public DelimitedLineTokenizer(string[] names, char token = DEFAULT_TOKEN)
        {
            _token = token;
            Headers = names;
        }

        public FieldSet Tokenize(string line)
        {
            string[] rowItems = line.Split(_token);
            return FieldSet.Create(Headers, rowItems);
        }

        public string[] Headers { get; private set; }
    }
}