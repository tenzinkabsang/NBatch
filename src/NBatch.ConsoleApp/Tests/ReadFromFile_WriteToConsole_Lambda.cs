using Microsoft.Extensions.Logging;
using NBatch.Core;
using NBatch.Readers.FileReader;

namespace NBatch.ConsoleApp.Tests;

public sealed class ReadFromFile_WriteToConsole_Lambda
{
    public static async Task RunAsync(string filePath, ILogger logger)
    {
        var job = Job.CreateBuilder(jobName: "JOB-LAMBDA")
            .WithLogger(logger)
            .AddStep("Import from file, uppercase with lambda, print to console", step => step
                .ReadFrom(new CsvReader<Product>(filePath, row => new Product
                {
                    Sku = row.GetString("ProductId"),
                    Name = row.GetString("Name"),
                    Description = row.GetString("Description"),
                    Price = row.GetDecimal("Price")
                }))
                .ProcessWith(p => new Product
                {
                    Sku = p.Sku.ToUpper(),
                    Name = p.Name.ToUpper(),
                    Description = p.Description.ToUpper(),
                    Price = p.Price
                })
                .WriteTo(items =>
                {
                    foreach (var item in items)
                        Console.WriteLine(item);
                    return Task.CompletedTask;
                }))
            .Build();

        var result = await job.RunAsync();

        Console.WriteLine($"Job '{result.Name}' completed: Success={result.Success}");
        foreach (var step in result.Steps)
        {
            Console.WriteLine($"  Step '{step.Name}': Read={step.ItemsRead}, Processed={step.ItemsProcessed}, ErrorsSkipped={step.ErrorsSkipped}");
        }
    }
}
