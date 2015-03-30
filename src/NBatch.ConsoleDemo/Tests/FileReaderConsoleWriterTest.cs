using NBatch.Main.Core;
using NBatch.Main.Readers.FileReader;

namespace NBatch.ConsoleDemo.Tests
{
    /// <summary>
    /// Read from a file, uppercase all values and write to console
    /// </summary>
    public class FileReaderConsoleWriterTest
    {
        public static void Run()
        {
            IStep processFileStep = new Step<Product, Product>("processFileStep")
                .SetReader(FlatFileReader())
                .SetProcessor(new ProductUppercaseProcessor())
                .SetWriter(new ConsoleWriter<Product>());

            new Job("Job1", "JobDB")
                .AddStep(processFileStep)
                .Start();
        }

        private static IReader<Product> FlatFileReader()
        {
            return new FlatFileItemBuilder<Product>(Program.SourceUrl, new ProductMapper())
                .WithHeaders(new[] { "ProductId", "Name", "Description", "Price" })
                .LinesToSkip(1)
                .Build();
        }
    }
}
