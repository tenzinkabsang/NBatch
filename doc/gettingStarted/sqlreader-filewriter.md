## Reading from Database and writing to file
```C#
/// <summary>
/// Read from database, lowercase all values and save it to a csv file.
/// </summary>
public class SqlReaderFileWriterTest
{
    public static void Run()
    {
        IStep processDb = new Step<Product, Product>("dbProcessor")
            .SetReader(SqlReader())
            .SetProcessor(new ProductLowercaseProcessor())
            .SetWriter(FlatFileWriter())
            .WithChunkSize(3);

        new Job("Job5", "JobDB")
            .AddStep(processDb)
            .Start();
    }

    private static IReader<Product> SqlReader()
    {
        return new SqlDbItemReader<Product>("ApplicationDB")
            .Query("Select * from Product")
            .OrderBy("ProductId");
    }

    private static IWriter<Product> FlatFileWriter()
    {
        string destPath = @"c:\dest";
        return new FlatFileItemWriter<Product>(destPath)
                    .WithToken(',');
    }
}
```