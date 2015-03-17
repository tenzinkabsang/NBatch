using NBatch.Core;
using NBatch.Core.Reader.FileReader;
using NBatch.Core.Reader.FileReader.Extensions;
using System;

namespace NBatch.ConsoleDemo
{
    class Program
    {
        static void Main(string[] args)
        {
            const string url = @"sample.txt";

            IStep processFileStep = new Step<Product, Product>("processFileStep")
                .UseFlatFileItemReader(
                    resourceUrl: url,
                    fieldMapper: new ProductMapper(),
                    linesToSkip: 1,
                    headers: new[] { "ProductId", "Name", "Description", "Price" })
                .WithChunkSize(5)
                .SkipLimit(10)
                .SkippableExceptions(typeof(FlatFileParseException))
                .SetProcessor(new ProductUppercaseProcessor())
                .SetWriter(new ConsoleWriter<Product>());

            bool result = new Job()
                .AddStep(processFileStep)
                .Start();

            Console.WriteLine("Done with result: {0}", result);
        }
    }
}
