using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NBatch.Core;
using NBatch.Readers.DbReader;
using NBatch.Writers.DbWriter;

namespace NBatch.ConsoleApp.Tests;

public class ReadFromDb_SaveToDb
{
    public static async Task RunAsync(string jobDbConnString, DbContext sourceDb, DbContext destinationDb, ILogger logger)
    {
        var job = Job.CreateBuilder(jobName: "JOB-2")
            .UseJobStore(jobDbConnString, DatabaseProvider.SqlServer)
            .WithLogger(logger)
            .AddStep("Read from DB and save to DB", step => step
                .ReadFrom(new DbReader<Product>(sourceDb, q => q.OrderBy(p => p.Sku)))
                .ProcessWith(p => new ProductLowercase
                {
                    Sku = p.Sku.ToLower(),
                    Name = p.Name.ToLower(),
                    Description = p.Description.ToLower(),
                    Price = p.Price
                })
                .WriteTo(new DbWriter<ProductLowercase>(destinationDb))
                .WithSkipPolicy(SkipPolicy.For<TimeoutException>(maxSkips: 3))
                .WithChunkSize(3))
            .Build();

        await job.RunAsync();
    }
}

