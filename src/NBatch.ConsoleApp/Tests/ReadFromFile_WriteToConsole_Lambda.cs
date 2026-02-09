using NBatch.Core;
using NBatch.Core.Interfaces;
using NBatch.Readers.FileReader;

namespace NBatch.ConsoleApp.Tests;

public sealed class ReadFromFile_WriteToConsole_Lambda
{
    public static async Task RunAsync(string filePath)
    {
        var job = Job.CreateBuilder(jobName: "JOB-LAMBDA")
            .AddStep("Import from file, uppercase with lambda, print to console")
            .ReadFrom(FileReader(filePath))
            .WriteTo(new ConsoleWriter<Product>())
            .ProcessWith(p => p with
            {
                Sku = p.Sku.ToUpper(),
                Name = p.Name.ToUpper(),
                Description = p.Description.ToUpper()
            })
            .Build();

        var result = await job.RunAsync();

        Console.WriteLine($"Job '{result.Name}' completed: Success={result.Success}");
        foreach (var step in result.Steps)
        {
            Console.WriteLine($"  Step '{step.Name}': Read={step.ItemsRead}, Processed={step.ItemsProcessed}, ErrorsSkipped={step.ErrorsSkipped}");
        }
    }

    private static IReader<Product> FileReader(string filePath) =>
        new FlatFileItemBuilder<Product>(filePath, new ProductMapper())
            .WithHeaders("Sku", "Name", "Description", "Price")
            .WithLinesToSkip(1)
            .Build();
}
