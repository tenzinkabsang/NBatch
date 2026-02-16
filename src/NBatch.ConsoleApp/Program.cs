using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NBatch.ConsoleApp;
using NBatch.ConsoleApp.Tests;
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

var fileSourcePath = PathUtil.GetPath(@"Files\NewItems\sample.txt");
var fileTargetPath = PathUtil.GetPath(@"Files\Processed\target.txt");

var jobDb = config["ConnectionStrings:JobDb"]!;
var appDbConnString = config["ConnectionStrings:AppDb"]!;

while (true)
{
    Console.WriteLine();
    Console.WriteLine("╔══════════════════════════════════════════╗");
    Console.WriteLine("║            NBatch Test Runner            ║");
    Console.WriteLine("╠══════════════════════════════════════════╣");
    Console.WriteLine("║  1. DB   -> DB                           ║");
    Console.WriteLine("║  2. DB   -> File                         ║");
    Console.WriteLine("║  3. File -> DB                           ║");
    Console.WriteLine("║  4. File -> Console                      ║");
    Console.WriteLine("║  5. File -> File                         ║");
    Console.WriteLine("║  6. File -> Console (lambda, no SQL)     ║");
    Console.WriteLine("║  7. File -> Console (DI)                 ║");
    Console.WriteLine("║  8. File -> Console (DI + ServiceProvider)║");
    Console.WriteLine("║  0. Exit                                 ║");
    Console.WriteLine("╚══════════════════════════════════════════╝");
    Console.Write("Select a test to run: ");

    var choice = Console.ReadLine()?.Trim();

    if (choice is "0" or null)
        break;

    try
    {
        switch (choice)
        {
            case "1":
                using (var src = AppDbContext.Create(appDbConnString))
                using (var dst = AppDbContext.Create(appDbConnString))
                    await ReadFromDb_SaveToDb.RunAsync(jobDb, src, dst, logger);
                break;

            case "2":
                using (var src = AppDbContext.Create(appDbConnString))
                    await ReadFromDb_SaveToFile.RunAsync(jobDb, src, fileTargetPath, logger);
                break;

            case "3":
                using (var dst = AppDbContext.Create(appDbConnString))
                    await ReadFromFile_SaveToDatabase.RunAsync(jobDb, dst, fileSourcePath, logger);
                break;

            case "4":
                await ReadFromFile_WriteToConsole.RunAsync(jobDb, fileSourcePath, logger);
                break;

            case "5":
                await ReadFromFile_WriteToFile.RunAsync(jobDb, fileSourcePath, fileTargetPath, logger);
                break;

            case "6":
                await ReadFromFile_WriteToConsole_Lambda.RunAsync(fileSourcePath, logger);
                break;

            case "7":
                await DI_ReadFromFile_WriteToConsole.RunAsync(fileSourcePath);
                break;

            case "8":
                await DI_ReadFromFile_WriteToConsole_WithServiceProvider.RunAsync(fileSourcePath, loggerFactory);
                break;

            default:
                Console.WriteLine("Invalid selection.");
                continue;
        }

        Console.WriteLine("Done.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error: {ex.Message}");
    }
}

