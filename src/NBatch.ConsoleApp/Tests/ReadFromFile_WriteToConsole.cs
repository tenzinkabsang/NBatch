using Microsoft.Extensions.Logging;
using NBatch.Core;
using NBatch.Readers.FileReader;

namespace NBatch.ConsoleApp.Tests;

public sealed class ReadFromFile_WriteToConsole
{
    public static async Task RunAsync(string connectionString, string filePath, ILogger logger)
    {
        var job = Job.CreateBuilder(jobName: "JOB-1")
            .UseJobStore(connectionString, DatabaseProvider.SqlServer)
            .WithLogger(logger)
            .AddStep("Import from file and print to console", step => step
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
                .WriteTo(new ConsoleWriter<ProductLowercase>())
                .WithChunkSize(5))
            .Build();

        await job.RunAsync();
    }
}
