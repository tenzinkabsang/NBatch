using NBatch.Core;
using NBatch.Core.Interfaces;
using NBatch.Readers.SqlReader;
using NBatch.Writers.FileWriter;

namespace NBatch.ConsoleApp.Tests;

public class SqlReaderFileWriter
{
    public static async Task RunAsync()
    {
        var jobBuilder = Job.CreateBuilder(jobName: "JOB-2", connectionString: "");

        jobBuilder.AddStep(
            stepName: "Read from SQL and save to file",
            reader: SqlReader(),
            writer: FileWriter(),
            chunkSize: 3
            );

        var job = jobBuilder.Build();
        await job.RunAsync();
    }

    public static IReader<Product> SqlReader()
    {
        return new MsSqlReader<Product>(connectionString: "", sql: "SELECT * FROM Products");
    }

    public static IWriter<Product> FileWriter()
    {
        return new FlatFileItemWriter<Product>("path");
    }
}
