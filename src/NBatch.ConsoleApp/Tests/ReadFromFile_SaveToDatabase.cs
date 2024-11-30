using NBatch.Core;
using NBatch.Core.Interfaces;
using NBatch.Readers.FileReader;
using NBatch.Writers.SqlWriter;

namespace NBatch.ConsoleApp.Tests;

public sealed class ReadFromFile_SaveToDatabase
{
    public static async Task RunAsync(string connectionString, string filePath)
    {
        var jobBuilder = Job.CreateBuilder(jobName: "JOB-1", connectionString);

        jobBuilder.AddStep(
            stepName: "Import from file and save to database",
            reader: FileReader(filePath),
            writer: DbWriter(connectionString),
            processor: new ProductLowercaseProcessor(),
            skipPolicy: SkipPolicy,
            chunkSize: 10
            );

        var job = jobBuilder.Build();
        await job.RunAsync();
    }

    /// <summary>
    ///  Specifies the exceptions that are skippable (per batch) along with the skip limit.
    ///  Once the skip limit threshold is reached it will throw and the job will stop.
    /// </summary>
    private static SkipPolicy SkipPolicy => new([typeof(FlatFileParseException)], skipLimit: 3);

    /// <summary>
    /// Constructs the FileReader with information about the headers.
    /// </summary>
    /// <param name="filePath">Location of the file.</param>
    private static IReader<Product> FileReader(string filePath) =>
        new FlatFileItemBuilder<Product>(filePath, new ProductMapper())
            .WithHeaders("Sku", "Name", "Description", "Price")
            .LinesToSkip(1)
            .Build();

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