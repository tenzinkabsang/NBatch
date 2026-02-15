## Reading from file and writing to sql database
```C#
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

        new Job("Job3", "JobDB")
            .AddStep(processFileStep)
            .Start();
    }

    private static IReader<Product> FlatFileReader()
    {
		string resourceUrl = @"c:\sample.csv";
        return new FlatFileItemBuilder<Product>(resourceUrl, new ProductMapper())
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
```