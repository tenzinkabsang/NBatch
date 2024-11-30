using NBatch.Core;
using NBatch.Core.Interfaces;
using NBatch.Readers.SqlReader;
using NBatch.Writers.FileWriter;

namespace NBatch.ConsoleApp.Tests;

public class ReadFromDb_SaveToFile
{
    public static async Task RunAsync(string connectionString, string filePath)
    {
        var jobBuilder = Job.CreateBuilder(jobName: "JOB-2", connectionString);

        jobBuilder.AddStep(
            stepName: "Read from SQL and save to file",
            reader: DbReader(connectionString),
            writer: FileWriter(filePath),
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
    /// Writes to file with the values separated using the provided token.
    /// </summary>
    private static IWriter<Product> FileWriter(string filePath) 
        => new FlatFileItemWriter<Product>(filePath)
                .WithToken('|');
}

