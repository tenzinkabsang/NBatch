using NBatch.Core;
using NBatch.Core.ItemReader;
using NBatch.Core.ItemWriter;
using NBatch.Core.Reader.FileReader;
using NBatch.Core.Writer.SqlWriter;
using System;

namespace NBatch.ConsoleDemo
{
    class Program
    {
        static string SourceUrl = PathUtil.GetPath(@"Files\NewItems\sample.txt");

        static void Main(string[] args)
        {
            string sourceUrl = PathUtil.GetPath(@"Files\NewItems\sample.txt");

            // Step to process the file
            IStep processFileStep = new Step<Product, Product>("processFileStep")
                .SetReader(FlatFileReader())
                .SetProcessor(new ProductUppercaseProcessor())
                .SetWriter(SqlWriter());

            // Step to clean-up the file after previous step is done processing it
            IStep cleanUpStep = new CleanupStep(sourceUrl, @"Files\Processed");

            new Job("DemoJob", "NBatchDb")
                .AddStep(processFileStep)
                .AddStep(cleanUpStep)
                .Start();

            Console.WriteLine("Finished job");
        }

        private static IReader<Product> FlatFileReader()
        {
            return new FlatFileItemBuilder<Product>(SourceUrl, new ProductMapper())
                .WithHeaders(new[] {"ProductId", "Name", "Description", "Price"})
                .LinesToSkip(1)
                .Build();
        }

        private static IWriter<Product> SqlWriter()
        {
            return new SqlDbItemWriter<Product>("NBatchDb")
                        .SetSql("INSERT INTO Product (ProductId, Name, Description, Price) VALUES (@ProductId, @Name, @Description, @Price);");
        }
    }
}