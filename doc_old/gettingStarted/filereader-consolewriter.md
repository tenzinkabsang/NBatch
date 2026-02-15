## Reading from file and writing to console
```C#
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
		string resourceUrl = @"c:\sample.csv";
        return new FlatFileItemBuilder<Product>(resourceUrl, new ProductMapper())
            .WithHeaders(new[] { "ProductId", "Name", "Description", "Price" })
            .LinesToSkip(1)
            .Build();
    }
}
```