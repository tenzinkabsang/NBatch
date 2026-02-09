using Microsoft.Extensions.Configuration;
using NBatch.ConsoleApp;
using NBatch.ConsoleApp.Tests;

Console.WriteLine("Hello, NBatch!");

var fileSourcePath = PathUtil.GetPath(@"Files\NewItems\sample.txt");
var fileTargetPath = PathUtil.GetPath(@"Files\Processed\target.txt");

var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .Build();

var jobDb = config["ConnectionStrings:JobDb"]!;
var sourceDb = config["ConnectionStrings:AppDb"]!;
var destinationDb = config["ConnectionStrings:AppDb"]!;


// UNCOMMENT each lines below for testing.
// PLEASE ensure that the BatchJob and BatchStep database tables are reset after each run, because one of the features
// of NBatch is that it will not reprocess items that has already been processed :)
await ReadFromDb_SaveToDb.RunAsync(jobDb, sourceDb, destinationDb);

await ReadFromDb_SaveToFile.RunAsync(jobDb, sourceDb, fileTargetPath);

await ReadFromFile_SaveToDatabase.RunAsync(jobDb, sourceDb, fileSourcePath);

await ReadFromFile_WriteToConsole.RunAsync(jobDb, fileSourcePath);

await ReadFromFile_WriteToFile.RunAsync(jobDb, fileSourcePath, fileTargetPath);

// Uses a lambda processor and in-memory job tracking (no SQL required)
await ReadFromFile_WriteToConsole_Lambda.RunAsync(fileSourcePath);

