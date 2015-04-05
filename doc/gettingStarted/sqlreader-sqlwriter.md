## Reading from Database and writing to Database
```C#
/// <summary>
/// Read from Product table, lowercase all values and write into SaleProduct table.
/// </summary>
public class SqlReaderSqlWriterTest
{
    public static void Run()
    {
        IStep processDb = new Step<Product, Product>("dbProcessor")
            .SetReader(SqlReader())
            .SetProcessor(new ProductLowercaseProcessor())
            .SetWriter(SqlWriter())
            .WithChunkSize(3);

        new Job("Job6", "JobDB")
            .AddStep(processDb)
            .Start();
    }

    private static IReader<Product> SqlReader()
    {
        return new SqlDbItemReader<Product>("ApplicationDB")
            .Query("Select * from Product")
            .OrderBy("ProductId");
    }

    private static IWriter<Product> SqlWriter()
    {
        return new SqlDbItemWriter<Product>("ApplicationDB")
                    .Query("INSERT INTO SaleProduct (ProductId, Name, Description, Price) VALUES (@ProductId, @Name, @Description, @Price);");
    }
}
```