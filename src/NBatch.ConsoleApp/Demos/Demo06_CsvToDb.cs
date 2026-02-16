using Microsoft.Extensions.Logging;
using NBatch.Core;
using NBatch.Readers.FileReader;
using NBatch.Writers.DbWriter;

namespace NBatch.ConsoleApp.Demos;

/// <summary>
/// DEMO 6 — CSV ? DB with restart-from-failure  (requires SQL Server via docker-compose)
///
/// Reads a CSV file, transforms to lowercase, writes to SQL Server.
/// Uses UseJobStore so if the job fails mid-way, re-running it
/// resumes from the last successful chunk — not from the beginning.
///
/// Features: CsvReader, DbWriter, UseJobStore (restart-from-failure), SkipPolicy, multi-step
/// </summary>
public static class Demo06_CsvToDb
{
    public static async Task RunAsync(string jobDbConnStr, string appDbConnStr, string filePath, ILogger logger)
    {
        using var destinationDb = AppDbContext.Create(appDbConnStr);

        var job = Job.CreateBuilder("demo-06-csv-to-db")
            .UseJobStore(jobDbConnStr, DatabaseProvider.SqlServer)
            .WithLogger(logger)
            .AddStep("import-csv-to-db", step => step
                .ReadFrom(new CsvReader<Product>(filePath, row => new Product
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
                .WriteTo(new DbWriter<ProductLowercase>(destinationDb))
                .WithSkipPolicy(SkipPolicy.For<FlatFileParseException>(maxSkips: 3))
                .WithChunkSize(10))
            .Build();

        var result = await job.RunAsync();

        Console.WriteLine($"  Job '{result.Name}' — Success: {result.Success}");
        foreach (var step in result.Steps)
            Console.WriteLine($"    Step '{step.Name}': Read={step.ItemsRead}, Processed={step.ItemsProcessed}, Skipped={step.ErrorsSkipped}");
    }
}
