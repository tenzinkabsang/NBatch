using NBatch.Core;
using NBatch.Core.Interfaces;
using NBatch.Readers.FileReader;

namespace NBatch.ConsoleApp.Tests;

public sealed class ReadFromFile_WriteToConsole
{
    public static async Task RunAsync(string connectionString, string filePath)
    {
        var jobBuilder = Job.CreateBuilder(jobName: "JOB-1", connectionString);

        jobBuilder.AddStep(
            stepName: "Import from file and print to console",
            reader: FileReader(filePath),
            writer: new ConsoleWriter<Product>(),
            processor: new ProductLowercaseProcessor()
            );

        var job = jobBuilder.Build();
        await job.RunAsync();
    }

    private static IReader<Product> FileReader(string filePath) =>
        new FlatFileItemBuilder<Product>(filePath, new ProductMapper())
            .WithHeaders("Sku", "Name", "Description", "Price")
            .LinesToSkip(1)
            .Build();
}
