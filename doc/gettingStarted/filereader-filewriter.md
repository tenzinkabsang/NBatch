## Reading from file and writing to a file
```C#
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
		string resourceUrl = @"c:\sample.csv";
        return new FlatFileItemBuilder<Product>(resourceUrl, new ProductMapper())
            .WithHeaders(new[] { "ProductId", "Name", "Description", "Price" })
            .LinesToSkip(1)
            .Build();
    }

    private static IWriter<Product> FlatFileWriter()
    {
		string destUrl = @"c:\dest";
        return new FlatFileItemWriter<Product>(destUrl)
                    .WithToken('|');
    }
}
```