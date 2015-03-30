using NBatch.Main.Core;
using NBatch.Main.Readers.FileReader;
using NBatch.Main.Writers.SqlWriter;

namespace NBatch.ConsoleDemo.Tests
{
    /// <summary>
    /// Read from file, uppercase all values and write to database.
    /// </summary>
    public class FileReaderSqlWriterTest
    {
        public static void Run()
        {
            IStep processFileStep = new Step<Product, Product>("processFileStep")
                .SetReader(FlatFileReader())
                .SetProcessor(new ProductUppercaseProcessor())
                .SetWriter(SqlWriter());

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

        private static IWriter<Product> SqlWriter()
        {
            return new SqlDbItemWriter<Product>("ApplicationDB")
                        .Query("INSERT INTO Product (ProductId, Name, Description, Price) VALUES (@ProductId, @Name, @Description, @Price);");
        }
    }
}