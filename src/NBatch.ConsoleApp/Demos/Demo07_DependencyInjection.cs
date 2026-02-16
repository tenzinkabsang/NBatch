using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NBatch.Core;
using NBatch.Core.Interfaces;
using NBatch.Readers.FileReader;

namespace NBatch.ConsoleApp.Demos;

/// <summary>
/// DEMO 7 — Dependency Injection with IServiceCollection
///
/// Registers an NBatch job via AddNBatch + IServiceCollection.
/// Resolves IProcessor, IWriter, and ILoggerFactory from the DI container
/// using the Action&lt;IServiceProvider, JobBuilder&gt; overload.
/// Runs the job on-demand via IJobRunner.
///
/// Features: AddNBatch, AddJob(sp, job), IJobRunner, service resolution
/// </summary>
public static class Demo07_DependencyInjection
{
    public static async Task RunAsync(string filePath, ILoggerFactory loggerFactory)
    {
        var services = new ServiceCollection();

        // Register application services
        services.AddSingleton(loggerFactory);
        services.AddSingleton<IProcessor<Product, Product>, ProductUppercaseProcessor>();
        services.AddSingleton<IWriter<Product>>(new ConsoleWriter<Product>());

        // Register NBatch with DI — resolve dependencies from the container
        services.AddNBatch(nbatch =>
        {
            nbatch.AddJob("demo-07-di", (sp, job) => job
                .WithLogger(sp.GetRequiredService<ILoggerFactory>().CreateLogger("NBatch"))
                .AddStep("import-via-di", step => step
                    .ReadFrom(new CsvReader<Product>(filePath, row => new Product
                    {
                        Sku = row.GetString("Sku"),
                        Name = row.GetString("Name"),
                        Description = row.GetString("Description"),
                        Price = row.GetDecimal("Price")
                    }))
                    .ProcessWith(sp.GetRequiredService<IProcessor<Product, Product>>())
                    .WriteTo(sp.GetRequiredService<IWriter<Product>>())
                    .WithChunkSize(5)));
        });

        // Resolve IJobRunner and run the job
        await using var sp = services.BuildServiceProvider();
        var runner = sp.GetRequiredService<IJobRunner>();

        var result = await runner.RunAsync("demo-07-di");

        Console.WriteLine();
        Console.WriteLine($"  Job '{result.Name}' — Success: {result.Success}");
        foreach (var step in result.Steps)
            Console.WriteLine($"    Step '{step.Name}': Read={step.ItemsRead}, Processed={step.ItemsProcessed}, Skipped={step.ErrorsSkipped}");
    }
}
