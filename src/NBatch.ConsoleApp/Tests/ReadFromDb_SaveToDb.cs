using NBatch.Core;
using NBatch.Core.Interfaces;
using NBatch.Readers.SqlReader;
using NBatch.Writers.SqlWriter;

namespace NBatch.ConsoleApp.Tests;

public class ReadFromDb_SaveToDb
{
    public static async Task RunAsync(string jobDbConnString, string sourceConnString, string destinationConnString)
    {
        var jobBuilder = Job.CreateBuilder(jobName: "JOB-2", jobDbConnString);

        jobBuilder.AddStep(
            stepName: "Read from DB and save to DB",
            reader: DbReader(sourceConnString),
            writer: DbWriter(destinationConnString),
            processor: new ProductLowercaseProcessor(),
            skipPolicy: SkipPolicy,
            chunkSize: 3
            );

        var job = jobBuilder.Build();
        await job.RunAsync();
    }

    /// <summary>
    ///  Specifies the exceptions that are skippable (per batch) along with the skip limit.
    ///  Once the skip limit threshold is reached it will throw and the job will stop.
    /// </summary>
    private static SkipPolicy SkipPolicy => new([typeof(TimeoutException)], skipLimit: 3);

    private static IReader<Product> DbReader(string connectionString)
        => new MsSqlReader<Product>(connectionString, sql: "SELECT * FROM Products");

    /// <summary>
    /// Creates a new MsSqlWriter that will execute the SQL statement (per batch) in a transaction.
    /// </summary>
    private static IWriter<Product> DbWriter(string connectionString) 
        => new MsSqlWriter<Product>(connectionString,
                    """
                    INSERT INTO Product (Sku, Name, Description, Price)
                    VALUES (@Sku, @Name, @Description, @Price)
                    """);
}

