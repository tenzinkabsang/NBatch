using Microsoft.Extensions.Logging;
using NBatch.Core;
using NBatch.Readers.FileReader;
using NBatch.Writers.FileWriter;

namespace NBatch.ConsoleApp.Demos;

/// <summary>
/// DEMO 2 — CSV ? File
///
/// Reads a CSV, lowercases all fields via a lambda processor,
/// writes the result to a pipe-delimited flat file.
///
/// Features: CsvReader, FlatFileItemWriter, WithToken, ProcessWith(lambda)
/// </summary>
public static class Demo02_CsvToFile
{
    public static async Task RunAsync(string sourcePath, string destinationPath, ILogger logger)
    {
        var job = Job.CreateBuilder("demo-02-csv-to-file")
            .WithLogger(logger)
            .AddStep("lowercase-to-file", step => step
                .ReadFrom(new CsvReader<Product>(sourcePath, row => new Product
                {
                    Sku = row.GetString("Sku"),
                    Name = row.GetString("Name"),
                    Description = row.GetString("Description"),
                    Price = row.GetDecimal("Price")
                }))
                .ProcessWith(p => new ProductLowercase
                {
                    Sku = p.Sku.ToLower(),
                    Name = p.Name.ToLower(),
                    Description = p.Description.ToLower(),
                    Price = p.Price
                })
                .WriteTo(new FlatFileItemWriter<ProductLowercase>(destinationPath).WithToken('|'))
                .WithChunkSize(5))
            .Build();

        var result = await job.RunAsync();

        Console.WriteLine($"  Job '{result.Name}' — Success: {result.Success}");
        Console.WriteLine($"  Output written to: {destinationPath}");
    }
}
