using Microsoft.Extensions.Logging;
using NBatch.Core;
using NBatch.Readers.FileReader;

namespace NBatch.ConsoleApp.Demos;

/// <summary>
/// DEMO 3 — Skip Policy
///
/// Intentionally throws on items priced above $700 to demonstrate
/// SkipPolicy.For&lt;T&gt;() — the job continues processing remaining items
/// and reports skipped errors in the result.
///
/// Features: SkipPolicy.For&lt;T&gt;(), error tolerance, WithChunkSize
/// </summary>
public static class Demo03_SkipPolicy
{
    public static async Task RunAsync(string filePath, ILogger logger)
    {
        var job = Job.CreateBuilder("demo-03-skip-policy")
            .WithLogger(logger)
            .AddStep("filter-expensive-books", step => step
                .ReadFrom(new CsvReader<Product>(filePath, row => new Product
                {
                    Sku = row.GetString("Sku"),
                    Name = row.GetString("Name"),
                    Description = row.GetString("Description"),
                    Price = row.GetDecimal("Price")
                }))
                .ProcessWith(p =>
                {
                    if (p.Price > 700)
                        throw new InvalidOperationException($"Price ${p.Price} exceeds limit for '{p.Name}'");

                    return p;
                })
                .WriteTo(items =>
                {
                    foreach (var item in items)
                        Console.WriteLine($"    ? {item.Name} — ${item.Price}");
                    return Task.CompletedTask;
                })
                .WithSkipPolicy(SkipPolicy.For<InvalidOperationException>(maxSkips: 10))
                .WithChunkSize(5))
            .Build();

        var result = await job.RunAsync();

        Console.WriteLine();
        Console.WriteLine($"  Job '{result.Name}' — Success: {result.Success}");
        foreach (var step in result.Steps)
            Console.WriteLine($"    Step '{step.Name}': Read={step.ItemsRead}, Processed={step.ItemsProcessed}, Skipped={step.ErrorsSkipped}");
    }
}
