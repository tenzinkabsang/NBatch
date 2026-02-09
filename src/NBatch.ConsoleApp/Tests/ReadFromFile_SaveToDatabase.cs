using NBatch.Core;
using NBatch.Core.Interfaces;
using NBatch.Readers.FileReader;
using NBatch.Writers.SqlWriter;

namespace NBatch.ConsoleApp.Tests;

public sealed class ReadFromFile_SaveToDatabase
{
    public static async Task RunAsync(string jobDbConnString, string destinationConnString, string filePath)
    {
        var job = Job.CreateBuilder(jobName: "JOB-1", jobDbConnString, DatabaseProvider.SqlServer)
            .AddStep("Import from file and save to database")
            .ReadFrom(FileReader(filePath))
            .WriteTo(DbWriter(destinationConnString))
            .ProcessWith(new ProductLowercaseProcessor())
            .WithSkipPolicy(new SkipPolicy([typeof(FlatFileParseException)], skipLimit: 3))
            .WithChunkSize(10)
            .Build();

        await job.RunAsync();
    }

    private static IReader<Product> FileReader(string filePath) =>
        new FlatFileItemBuilder<Product>(filePath, new ProductMapper())
            .WithHeaders("Sku", "Name", "Description", "Price")
            .WithLinesToSkip(1)
            .Build();

    private static IWriter<Product> DbWriter(string connectionString) 
        => new MsSqlWriter<Product>(connectionString,
                """
                INSERT INTO Product (Sku, Name, Description, Price)
                VALUES (@Sku, @Name, @Description, @Price)
                """);
}