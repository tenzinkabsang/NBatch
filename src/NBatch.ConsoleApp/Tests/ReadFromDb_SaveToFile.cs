using Microsoft.EntityFrameworkCore;
using NBatch.Core;
using NBatch.Readers.DbReader;
using NBatch.Writers.FileWriter;

namespace NBatch.ConsoleApp.Tests;

public class ReadFromDb_SaveToFile
{
    public static async Task RunAsync(string jobDbConnString, DbContext sourceDb, string filePath)
    {
        var job = Job.CreateBuilder(jobName: "JOB-2")
            .UseJobStore(jobDbConnString, DatabaseProvider.SqlServer)
            .AddStep("Read from SQL and save to file", step => step
                .ReadFrom(new DbReader<Product>(sourceDb, q => q.OrderBy(p => p.Sku)))
                .WriteTo(FileWriter(filePath))
                .WithSkipPolicy(SkipPolicy.For<TimeoutException>(maxSkips: 3))
                .WithChunkSize(3))
            .Build();

        await job.RunAsync();
    }

    private static FlatFileItemWriter<Product> FileWriter(string filePath) 
        => new FlatFileItemWriter<Product>(filePath)
                .WithToken('|');
}

