using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NBatch.ConsoleApp;
using NBatch.ConsoleApp.Demos;
using Serilog;

var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .Build();

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(config)
    .CreateLogger();

using var loggerFactory = LoggerFactory.Create(builder => builder.AddSerilog(dispose: false));
var logger = loggerFactory.CreateLogger("NBatch");

var csvPath = PathUtil.GetPath(@"Files\NewItems\sample.txt");
var outputPath = PathUtil.GetPath(@"Files\Processed\target.txt");

var jobDb = config["ConnectionStrings:JobDb"]!;
var appDb = config["ConnectionStrings:AppDb"]!;

while (true)
{
    Console.WriteLine();
    Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
    Console.WriteLine("║                    NBatch Demo Runner                     ║");
    Console.WriteLine("╠═══════════════════════════════════════════════════════════╣");
    Console.WriteLine("║                                                           ║");
    Console.WriteLine("║  No database required:                                    ║");
    Console.WriteLine("║    1. CSV → Console       (minimal API, lambdas)          ║");
    Console.WriteLine("║    2. CSV → File           (FlatFileItemWriter)           ║");
    Console.WriteLine("║    3. CSV → Console       (SkipPolicy error handling)     ║");
    Console.WriteLine("║    4. Tasklet Steps        (Action + async tasklets)      ║");
    Console.WriteLine("║                                                           ║");
    Console.WriteLine("║  Requires SQL Server (docker-compose up):                 ║");
    Console.WriteLine("║    5. DB  → DB             (DbReader, DbWriter)           ║");
    Console.WriteLine("║    6. CSV → DB             (restart-from-failure)         ║");
    Console.WriteLine("║                                                           ║");
    Console.WriteLine("║  Dependency Injection:                                    ║");
    Console.WriteLine("║    7. DI + IJobRunner      (AddNBatch, IServiceProvider)  ║");
    Console.WriteLine("║    8. BackgroundService    (RunOnce, RunEvery)            ║");
    Console.WriteLine("║                                                           ║");
    Console.WriteLine("║    0. Exit                                                ║");
    Console.WriteLine("╚═══════════════════════════════════════════════════════════╝");
    Console.Write("Select a demo: ");

    var choice = Console.ReadLine()?.Trim();

    if (choice is "0" or null)
        break;

    Console.WriteLine();

    try
    {
        switch (choice)
        {
            case "1":
                await Demo01_CsvToConsole.RunAsync(csvPath, logger);
                break;

            case "2":
                await Demo02_CsvToFile.RunAsync(csvPath, outputPath, logger);
                break;

            case "3":
                await Demo03_SkipPolicy.RunAsync(csvPath, logger);
                break;

            case "4":
                await Demo04_TaskletSteps.RunAsync(outputPath, logger);
                break;

            case "5":
                await Demo05_DbToDb.RunAsync(jobDb, appDb, logger);
                break;

            case "6":
                await Demo06_CsvToDb.RunAsync(jobDb, appDb, csvPath, logger);
                break;

            case "7":
                await Demo07_DependencyInjection.RunAsync(csvPath, loggerFactory);
                break;

            case "8":
                await Demo08_BackgroundService.RunAsync(csvPath);
                break;

            default:
                Console.WriteLine("  Invalid selection.");
                continue;
        }

        Console.WriteLine();
        Console.WriteLine("  ✓ Demo complete.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  ✗ Error: {ex.Message}");
    }
}


