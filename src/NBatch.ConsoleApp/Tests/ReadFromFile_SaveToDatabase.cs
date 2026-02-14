using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NBatch.Core;
using NBatch.Readers.FileReader;
using NBatch.Writers.DbWriter;

namespace NBatch.ConsoleApp.Tests;

public sealed class ReadFromFile_SaveToDatabase
{
    public static async Task RunAsync(string jobDbConnString, DbContext destinationDb, string filePath, ILogger logger)
    {
        var job = Job.CreateBuilder(jobName: "JOB-1")
            .UseJobStore(jobDbConnString, DatabaseProvider.SqlServer)
            .WithLogger(logger)
            .AddStep("Import from file and save to database", step => step
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
                .WriteTo(new DbWriter<ProductLowercase>(destinationDb))
                .WithSkipPolicy(SkipPolicy.For<FlatFileParseException>(maxSkips: 3))
                .WithChunkSize(10))
            .Build();

        await job.RunAsync();
    }
}