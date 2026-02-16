using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NBatch.Core;
using NBatch.Readers.DbReader;
using NBatch.Writers.DbWriter;

namespace NBatch.ConsoleApp.Demos;

/// <summary>
/// DEMO 5 — DB ? DB with Job Store  (requires SQL Server via docker-compose)
///
/// Reads products from SQL Server, lowercases all fields via IProcessor,
/// writes to a destination table. Uses UseJobStore for restart-from-failure tracking.
///
/// Features: DbReader, DbWriter, IProcessor class, UseJobStore, SkipPolicy, WithChunkSize
/// </summary>
public static class Demo05_DbToDb
{
    public static async Task RunAsync(string jobDbConnStr, string appDbConnStr, ILogger logger)
    {
        using var sourceDb = AppDbContext.Create(appDbConnStr);
        using var destinationDb = AppDbContext.Create(appDbConnStr);

        var job = Job.CreateBuilder("demo-05-db-to-db")
            .UseJobStore(jobDbConnStr, DatabaseProvider.SqlServer)
            .WithLogger(logger)
            .AddStep("transform-products", step => step
                .ReadFrom(new DbReader<Product>(sourceDb, q => q.OrderBy(p => p.Sku)))
                .ProcessWith(new ProductLowercaseProcessor())
                .WriteTo(new DbWriter<ProductLowercase>(destinationDb))
                .WithSkipPolicy(SkipPolicy.For<TimeoutException>(maxSkips: 3))
                .WithChunkSize(5))
            .Build();

        var result = await job.RunAsync();

        Console.WriteLine($"  Job '{result.Name}' — Success: {result.Success}");
        foreach (var step in result.Steps)
            Console.WriteLine($"    Step '{step.Name}': Read={step.ItemsRead}, Processed={step.ItemsProcessed}, Skipped={step.ErrorsSkipped}");
    }
}
