using System;
using NBatch.Main.Core;
using NBatch.Main.Readers.FileReader;
using NBatch.Main.Readers.SqlReader;
using NBatch.Main.Writers.SqlWriter;

namespace NBatch.ConsoleDemo
{
    class Program
    {
        static readonly string SourceUrl = PathUtil.GetPath(@"Files\NewItems\sample.txt");

        static void Main(string[] args)
        {
           
            ExecuteJob1();

            ExecuteJob2();

            Console.WriteLine("Finished job");
        }

        private static void ExecuteJob1()
        {
            // Step to process the file
            IStep processFileStep = new Step<Product, Product>("processFileStep")
                .SetReader(FlatFileReader())
                .SetProcessor(new ProductUppercaseProcessor())
                .SetWriter(SqlWriter("Product"));

            // Step to clean-up the file after previous step is done processing it
            IStep cleanUpStep = new CleanupStep(SourceUrl, @"Files\Processed");

            new Job("Job1", "NBatchDb")
                .AddStep(processFileStep)
                //.AddStep(cleanUpStep)
                .Start();
        }

        private static void ExecuteJob2()
        {
            IStep processDb = new Step<Product, Product>("dbProcessor")
                .SetReader(SqlReader())
                .SetProcessor(new ProductLowercaseProcessor())
                .SetWriter(SqlWriter("SaleProduct"))
                .WithChunkSize(3);

            new Job("DemoJob2", "NBatchDb")
                .AddStep(processDb)
                .Start();
        }

        private static IReader<Product> FlatFileReader()
        {
            return new FlatFileItemBuilder<Product>(SourceUrl, new ProductMapper())
                .WithHeaders(new[] { "ProductId", "Name", "Description", "Price" })
                .LinesToSkip(1)
                .Build();
        }

        private static IWriter<Product> SqlWriter(string table)
        {
            return new SqlDbItemWriter<Product>("NBatchDb")
                        .Query(string.Format("INSERT INTO {0} (ProductId, Name, Description, Price) VALUES (@ProductId, @Name, @Description, @Price);", table));
        }

        private static IReader<Product> SqlReader()
        {
            return new SqlDbItemReader<Product>("NBatchDb")
                .Query("Select * from Product")
                .OrderBy("ProductId");
        }
    }
}