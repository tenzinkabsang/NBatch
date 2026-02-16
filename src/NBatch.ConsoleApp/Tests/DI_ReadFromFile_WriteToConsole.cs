using Microsoft.Extensions.DependencyInjection;
using NBatch.Core;
using NBatch.Readers.FileReader;

namespace NBatch.ConsoleApp.Tests;

public sealed class DI_ReadFromFile_WriteToConsole
{
    public static async Task RunAsync(string filePath)
    {
        var services = new ServiceCollection();

        services.AddNBatch(nbatch =>
        {
            nbatch.AddJob("file-to-console", job => job
                .AddStep("import", step => step
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
                            Console.WriteLine(item);
                        return Task.CompletedTask;
                    })
                    .WithChunkSize(5)));
        });

        await using var sp = services.BuildServiceProvider();
        var runner = sp.GetRequiredService<IJobRunner>();

        var result = await runner.RunAsync("file-to-console");

        Console.WriteLine($"Job '{result.Name}' completed: Success={result.Success}");
        foreach (var step in result.Steps)
        {
            Console.WriteLine($"  Step '{step.Name}': Read={step.ItemsRead}, Processed={step.ItemsProcessed}, ErrorsSkipped={step.ErrorsSkipped}");
        }
    }
}
