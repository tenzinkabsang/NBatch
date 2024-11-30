using NBatch.Core;
using NBatch.Core.Interfaces;
using NBatch.Readers.FileReader;

namespace NBatch.ConsoleApp.Tests;

public class FileReaderConsoleWriter
{
    public static async Task RunAsync()
    {
        var jobBuilder = Job.CreateBuilder(jobName: "JOB-1", connectionString: "");

        jobBuilder.AddStep(
            stepName: "Import from file and print to console",
            reader: FileReader(),
            writer: new ConsoleWriter<Product>(),
            processor: new ProductLowercaseProcessor()
            );

        var job = jobBuilder.Build();
        await job.RunAsync();
    }

    private static IReader<Product> FileReader() =>
        new FlatFileItemBuilder<Product>(resourceUrl: "", new ProductMapper())
            .WithHeaders("Id", "Name", "Description", "Price")
            .LinesToSkip(1)
            .Build();
}
