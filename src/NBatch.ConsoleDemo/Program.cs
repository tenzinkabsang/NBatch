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
            string sourceUrl = PathUtil.GetPath(@"Files\NewItems\sample.txt");

            // Step to process the file
            IStep processFileStep = new Step<Product, Product>("processFileStep")
                .UseFlatFileItemReader(
                    resourceUrl: sourceUrl,
                    fieldMapper: new ProductMapper(),
                    linesToSkip: 1,
                    headers: new[] { "ProductId", "Name", "Description", "Price" })
                .WithChunkSize(5)
                .SkipLimit(2)
                .SkippableExceptions(typeof(FlatFileParseException))
                .SetProcessor(new ProductUppercaseProcessor())
                .SetWriter(new ConsoleWriter<Product>());

            // Step to clean-up the file after previous step is done processing it
            IStep cleanUpStep = new CleanupStep(sourceUrl, @"Files\\Processed");

            new Job()
                .AddStep(processFileStep)
                .AddStep(cleanUpStep)
                .Start();

            Console.WriteLine("Finished job");
        }
    }
}
