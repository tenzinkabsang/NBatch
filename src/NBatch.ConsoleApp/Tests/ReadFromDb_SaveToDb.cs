using Microsoft.EntityFrameworkCore;
using NBatch.Core;
using NBatch.Readers.DbReader;
using NBatch.Writers.DbWriter;

namespace NBatch.ConsoleApp.Tests;

public class ReadFromDb_SaveToDb
{
    public static async Task RunAsync(string jobDbConnString, DbContext sourceDb, DbContext destinationDb)
    {
        var job = Job.CreateBuilder(jobName: "JOB-2", jobDbConnString, DatabaseProvider.SqlServer)
            .AddStep("Read from DB and save to DB")
            .ReadFrom(new DbReader<Product>(sourceDb, q => q.OrderBy(p => p.Sku)))
            .WriteTo(new DbWriter<ProductLowercase>(destinationDb))
            .ProcessWith(new ProductLowercaseProcessor())
            .WithSkipPolicy(new SkipPolicy([typeof(TimeoutException)], skipLimit: 3))
            .WithChunkSize(3)
            .Build();

        await job.RunAsync();
    }
}

