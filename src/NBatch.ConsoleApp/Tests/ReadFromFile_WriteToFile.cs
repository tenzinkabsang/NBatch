using NBatch.Core;
using NBatch.Core.Interfaces;
using NBatch.Readers.FileReader;
using NBatch.Writers.FileWriter;

namespace NBatch.ConsoleApp.Tests;

public sealed class ReadFromFile_WriteToFile
{
    public static async Task RunAsync(string connectionString, string sourcePath, string destinationPath)
    {
        var jobBuilder = Job.CreateBuilder(jobName: "JOB1", connectionString);

        jobBuilder.AddStep(
            stepName: "Import from file, lowercase the properties and save to file",
            reader: FileReader(sourcePath),
            writer: FileWriter(destinationPath),
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

    private static IWriter<Product> FileWriter(string filePath)
        => new FlatFileItemWriter<Product>(filePath);
}