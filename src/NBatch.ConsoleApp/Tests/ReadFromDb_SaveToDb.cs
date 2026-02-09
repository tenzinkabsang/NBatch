using NBatch.Core;
using NBatch.Core.Interfaces;
using NBatch.Readers.SqlReader;
using NBatch.Writers.SqlWriter;

namespace NBatch.ConsoleApp.Tests;

public class ReadFromDb_SaveToDb
{
    public static async Task RunAsync(string jobDbConnString, string sourceConnString, string destinationConnString)
    {
        var job = Job.CreateBuilder(jobName: "JOB-2", jobDbConnString, DatabaseProvider.SqlServer)
            .AddStep("Read from DB and save to DB")
            .ReadFrom(DbReader(sourceConnString))
            .WriteTo(DbWriter(destinationConnString))
            .ProcessWith(new ProductLowercaseProcessor())
            .WithSkipPolicy(new SkipPolicy([typeof(TimeoutException)], skipLimit: 3))
            .WithChunkSize(3)
            .Build();

        await job.RunAsync();
    }

    private static IReader<Product> DbReader(string connectionString)
        => new MsSqlReader<Product>(connectionString, sql: "SELECT * FROM Products ORDER BY Sku");

    private static IWriter<Product> DbWriter(string connectionString) 
        => new MsSqlWriter<Product>(connectionString,
                    """
                    INSERT INTO Product (Sku, Name, Description, Price)
                    VALUES (@Sku, @Name, @Description, @Price)
                    """);
}

