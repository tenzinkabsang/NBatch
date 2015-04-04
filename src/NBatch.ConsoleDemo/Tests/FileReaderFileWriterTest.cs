using System;
using System.IO;
using NBatch.Main.Core;
using NBatch.Main.Readers.FileReader;
using NBatch.Main.Writers.FileWriter;

namespace NBatch.ConsoleDemo.Tests
{
    /// <summary>
    /// Read from a csv (default token) file, uppercase all values and save to a new file separated with '|'.
    /// </summary>
    public class FileReaderFileWriterTest
    {
        public static void Run()
        {
            IStep processFileStep = new Step<Product, Product>("processFileStep")
                .SetReader(FlatFileReader())
                .SetProcessor(new ProductUppercaseProcessor())
                .SetWriter(FlatFileWriter());

            new Job("Job2", "JobDB")
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

        private static IWriter<Product> FlatFileWriter()
        {
            string destPath = GetRelativeFilePath();

            return new FlatFileItemWriter<Product>(destPath)
                        .WithToken('|');
        }

        private static string GetRelativeFilePath()
        {
            string target = PathUtil.GetPath(@"Files\Processed");
            return Path.Combine(target, "fileReaderFileWriter.txt");
        }
    }
}