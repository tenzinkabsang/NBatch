using Microsoft.Extensions.Logging;
using NBatch.Core;
using NBatch.Readers.FileReader;

namespace NBatch.ConsoleApp.Demos;

/// <summary>
/// DEMO 1 — Minimal API
///
/// Shows the simplest possible NBatch job:
///   CsvReader ? lambda processor ? lambda writer
///   No database, no DI, no SQL — just read, transform, print.
///
/// Features: Job.CreateBuilder, CsvReader, ProcessWith(lambda), WriteTo(lambda)
/// </summary>
public static class Demo01_CsvToConsole
{
    public static async Task RunAsync(string filePath, ILogger logger)
    {
        var job = Job.CreateBuilder("demo-01-csv-to-console")
            .WithLogger(logger)
            .AddStep("read-and-print", step => step
                .ReadFrom(new CsvReader<Product>(filePath, row => new Product
                {
                    Sku = row.GetString("Sku"),
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
                        Console.WriteLine($"    {item}");
                    return Task.CompletedTask;
                })
                .WithChunkSize(5))
            .Build();

        var result = await job.RunAsync();
        PrintResult(result);
    }

    private static void PrintResult(JobResult result)
    {
        Console.WriteLine();
        Console.WriteLine($"  Job '{result.Name}' — Success: {result.Success}");
        foreach (var step in result.Steps)
            Console.WriteLine($"    Step '{step.Name}': Read={step.ItemsRead}, Processed={step.ItemsProcessed}, Skipped={step.ErrorsSkipped}");
    }
}
