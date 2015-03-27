using NBatch.Main.Core;
using NBatch.Main.Readers.FileReaders.Services;

namespace NBatch.Main.Readers.FileReaders.Extensions
{
    public static class FlatFileItemReaderExt
    {
        public static Step<TInput, TOutput> UseFlatFileItemReader<TInput, TOutput>(this Step<TInput, TOutput> step,
            string resourceUrl, IFieldSetMapper<TInput> fieldMapper, char token = DelimitedLineTokenizer.DEFAULT_TOKEN)
        {
            IReader<TInput> flatFileItemReader = CreateFlatFileReader(resourceUrl, fieldMapper, new DelimitedLineTokenizer(token));
            return step.SetReader(flatFileItemReader);
        }

        public static Step<TInput, TOutput> UseFlatFileItemReader<TInput, TOutput>(this Step<TInput, TOutput> step,
            string resourceUrl, IFieldSetMapper<TInput> fieldMapper, int linesToSkip, string[] headers, char token = DelimitedLineTokenizer.DEFAULT_TOKEN)
        {
            IReader<TInput> flatFileItemReader = CreateFlatFileReader(resourceUrl, fieldMapper, new DelimitedLineTokenizer(headers, token));
            flatFileItemReader.LinesToSkip = linesToSkip;
            return step.SetReader(flatFileItemReader);
        }

        private static IReader<TInput> CreateFlatFileReader<TInput>(string resourceUrl, IFieldSetMapper<TInput> fieldSetMapper, ILineTokenizer tokenizer)
        {
            var lineMapper = new DefaultLineMapper<TInput>(tokenizer, fieldSetMapper);
            return new FlatFileItemReader<TInput>(lineMapper, new FileService(resourceUrl));
        }
    }
}
