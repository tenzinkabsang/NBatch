using Microsoft.EntityFrameworkCore;
using NBatch.Core;
using NBatch.Core.Interfaces;
using NBatch.Readers.FileReader;
using NBatch.Writers.DbWriter;

namespace NBatch.ConsoleApp.Tests;

public sealed class ReadFromFile_SaveToDatabase
{
    public static async Task RunAsync(string jobDbConnString, DbContext destinationDb, string filePath)
    {
        var job = Job.CreateBuilder(jobName: "JOB-1", jobDbConnString, DatabaseProvider.SqlServer)
            .AddStep("Import from file and save to database")
            .ReadFrom(FileReader(filePath))
            .WriteTo(new DbWriter<ProductLowercase>(destinationDb))
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
}