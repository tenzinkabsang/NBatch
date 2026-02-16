using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NBatch.Core;
using NBatch.Core.Interfaces;
using NBatch.Readers.FileReader;

namespace NBatch.ConsoleApp.Tests;

public sealed class DI_ReadFromFile_WriteToConsole_WithServiceProvider
{
    public static async Task RunAsync(string filePath, ILoggerFactory loggerFactory)
    {
        var services = new ServiceCollection();

        // Register application services — readers, processors, writers
        services.AddSingleton(loggerFactory);
        services.AddSingleton<IProcessor<Product, Product>, ProductUppercaseProcessor>();
        services.AddSingleton<IWriter<Product>>(new ConsoleWriter<Product>());

        services.AddNBatch(nbatch =>
        {
            nbatch.AddJob("file-to-console-di", (sp, job) => job
                .WithLogger(sp.GetRequiredService<ILoggerFactory>().CreateLogger("NBatch"))
                .AddStep("import", step => step
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

        await using var sp = services.BuildServiceProvider();
        var runner = sp.GetRequiredService<IJobRunner>();

        var result = await runner.RunAsync("file-to-console-di");

        Console.WriteLine($"Job '{result.Name}' completed: Success={result.Success}");
        foreach (var step in result.Steps)
        {
            Console.WriteLine($"  Step '{step.Name}': Read={step.ItemsRead}, Processed={step.ItemsProcessed}, ErrorsSkipped={step.ErrorsSkipped}");
        }
    }
}
