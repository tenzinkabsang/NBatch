using Microsoft.EntityFrameworkCore;
using NBatch.Core;
using NBatch.Core.Interfaces;
using NBatch.Readers.DbReader;
using NBatch.Writers.FileWriter;

namespace NBatch.ConsoleApp.Tests;

public class ReadFromDb_SaveToFile
{
    public static async Task RunAsync(string jobDbConnString, DbContext sourceDb, string filePath)
    {
        var job = Job.CreateBuilder(jobName: "JOB-2", jobDbConnString, DatabaseProvider.SqlServer)
            .AddStep("Read from SQL and save to file")
            .ReadFrom(new DbReader<Product>(sourceDb, q => q.OrderBy(p => p.Sku)))
            .WriteTo(FileWriter(filePath))
            .WithSkipPolicy(new SkipPolicy([typeof(TimeoutException)], skipLimit: 3))
            .WithChunkSize(3)
            .Build();

        await job.RunAsync();
    }

    private static IWriter<Product> FileWriter(string filePath) 
        => new FlatFileItemWriter<Product>(filePath)
                .WithToken('|');
}

