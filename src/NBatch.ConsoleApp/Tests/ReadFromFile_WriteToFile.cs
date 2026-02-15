using Microsoft.Extensions.Logging;
using NBatch.Core;
using NBatch.Readers.FileReader;
using NBatch.Writers.FileWriter;

namespace NBatch.ConsoleApp.Tests;

public sealed class ReadFromFile_WriteToFile
{
    public static async Task RunAsync(string connectionString, string sourcePath, string destinationPath, ILogger logger)
    {
        var job = Job.CreateBuilder(jobName: "JOB1")
            .UseJobStore(connectionString, DatabaseProvider.SqlServer)
            .WithLogger(logger)
            .AddStep("Import from file, lowercase the properties and save to file", step => step
                .ReadFrom(new CsvReader<Product>(sourcePath, row => new Product
                {
                    Sku = row.GetString("ProductId"),
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
                .WriteTo(new FlatFileItemWriter<ProductLowercase>(destinationPath)))
            .Build();

        await job.RunAsync();
    }
}