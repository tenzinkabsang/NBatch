## Reading from Database and writing to console
```C#
/// <summary>
/// Read from database, lowercase all values and write to console.
/// </summary>
public class SqlReaderConsoleWriterTest
{
    public static void Run()
    {
        IStep processDb = new Step<Product, Product>("dbProcessor")
            .SetReader(SqlReader())
            .SetProcessor(new ProductLowercaseProcessor())
            .SetWriter(new ConsoleWriter<Product>())
            .WithChunkSize(3);

        new Job("Job4", "JobDB")
            .AddStep(processDb)
            .Start();
    }

    private static IReader<Product> SqlReader()
    {
        return new SqlDbItemReader<Product>("ApplicationDB")
            .Query("Select * from Product")
            .OrderBy("ProductId");
    }
}
```