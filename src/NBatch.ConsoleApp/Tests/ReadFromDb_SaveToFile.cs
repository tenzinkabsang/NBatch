using NBatch.Core;
using NBatch.Core.Interfaces;
using NBatch.Readers.SqlReader;
using NBatch.Writers.FileWriter;

namespace NBatch.ConsoleApp.Tests;

public class ReadFromDb_SaveToFile
{
    public static async Task RunAsync(string jobDbConnString, string sourceConnString, string filePath)
    {
        var job = Job.CreateBuilder(jobName: "JOB-2", jobDbConnString, DatabaseProvider.SqlServer)
            .AddStep("Read from SQL and save to file")
            .ReadFrom(DbReader(sourceConnString))
            .WriteTo(FileWriter(filePath))
            .WithSkipPolicy(new SkipPolicy([typeof(TimeoutException)], skipLimit: 3))
            .WithChunkSize(3)
            .Build();

        await job.RunAsync();
    }

    private static IReader<Product> DbReader(string connectionString) 
        => new MsSqlReader<Product>(connectionString, sql: "SELECT * FROM Products ORDER BY Sku");

    private static IWriter<Product> FileWriter(string filePath) 
        => new FlatFileItemWriter<Product>(filePath)
                .WithToken('|');
}

