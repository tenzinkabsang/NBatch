using System.Linq;

namespace NBatch.Main.Readers.FileReader
{
    sealed class DelimitedLineTokenizer : ILineTokenizer
    {
        private readonly char _token;
        public const char DEFAULT_TOKEN = ',';
        
        public DelimitedLineTokenizer(char token = DEFAULT_TOKEN)
            :this(null, token)
        {
        }

        public DelimitedLineTokenizer(string[] names, char? token)
        {
            Headers = names ?? Enumerable.Empty<string>().ToArray();
            _token = token ?? DEFAULT_TOKEN;
        }

        public FieldSet Tokenize(string line)
        {
            string[] rowItems = line.Split(_token);
            return FieldSet.Create(Headers, rowItems);
        }

        public string[] Headers { get; private set; }
    }
}