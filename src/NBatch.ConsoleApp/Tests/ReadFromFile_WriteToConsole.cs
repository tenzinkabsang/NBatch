using NBatch.Core;
using NBatch.Readers.FileReader;

namespace NBatch.ConsoleApp.Tests;

public sealed class ReadFromFile_WriteToConsole
{
    public static async Task RunAsync(string connectionString, string filePath)
    {
        var job = Job.CreateBuilder(jobName: "JOB-1")
            .UseJobStore(connectionString, DatabaseProvider.SqlServer)
            .AddStep("Import from file and print to console", step => step
                .ReadFrom(new CsvReader<Product>(filePath, row => new Product
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
                .WriteTo(new ConsoleWriter<ProductLowercase>()))
            .Build();

        await job.RunAsync();
    }
}
