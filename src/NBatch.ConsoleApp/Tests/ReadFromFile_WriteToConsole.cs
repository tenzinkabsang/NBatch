using NBatch.Core;
using NBatch.Core.Interfaces;
using NBatch.Readers.FileReader;

namespace NBatch.ConsoleApp.Tests;

public sealed class ReadFromFile_WriteToConsole
{
    public static async Task RunAsync(string connectionString, string filePath)
    {
        var job = Job.CreateBuilder(jobName: "JOB-1", connectionString, DatabaseProvider.SqlServer)
            .AddStep("Import from file and print to console")
            .ReadFrom(FileReader(filePath))
            .WriteTo(new ConsoleWriter<ProductLowercase>())
            .ProcessWith(new ProductLowercaseProcessor())
            .Build();

        await job.RunAsync();
    }

    private static IReader<Product> FileReader(string filePath) =>
        new FlatFileItemBuilder<Product>(filePath, new ProductMapper())
            .WithHeaders("Sku", "Name", "Description", "Price")
            .WithLinesToSkip(1)
            .Build();
}
