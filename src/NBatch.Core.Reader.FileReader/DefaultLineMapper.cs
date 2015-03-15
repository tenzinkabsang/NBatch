namespace NBatch.Core.Reader.FileReader
{
    sealed class DefaultLineMapper<T>: ILineMapper<T>
    {
        private readonly IFieldSetMapper<T> _mapper;
        public ILineTokenizer Tokenizer { get; private set; }

        public DefaultLineMapper(ILineTokenizer tokenizer, IFieldSetMapper<T> mapper)
        {
            _mapper = mapper;
            Tokenizer = tokenizer;
        }

        public T MapToModel(string line)
        {
            // Call tokenizer to return a fieldset
            FieldSet fieldSet = Tokenizer.Tokenize(line);

            // Call Mapper passing in the fieldset
            T result = _mapper.MapFieldSet(fieldSet);

            return result;
        }
    }
}