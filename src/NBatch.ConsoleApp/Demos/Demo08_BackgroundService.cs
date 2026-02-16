using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NBatch.Core;
using NBatch.Readers.FileReader;
using Serilog;

namespace NBatch.ConsoleApp.Demos;

/// <summary>
/// DEMO 8 — BackgroundService with RunOnce / RunEvery
///
/// Builds a full .NET Generic Host with two scheduled NBatch jobs:
///   - "startup-import" runs once on startup (RunOnce)
///   - "periodic-check" runs every 3 seconds (RunEvery)
///
/// The host runs for 10 seconds, then shuts down gracefully.
/// Both jobs remain available on-demand via IJobRunner.
///
/// Features: AddNBatch, RunOnce(), RunEvery(), BackgroundService, IHostedService, Generic Host
/// </summary>
public static class Demo08_BackgroundService
{
    public static async Task RunAsync(string filePath)
    {
        Console.WriteLine("  Starting Generic Host with two scheduled jobs...");
        Console.WriteLine("  (will run for 10 seconds then shut down)");
        Console.WriteLine();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var host = Host.CreateDefaultBuilder()
            .UseSerilog((context, config) => config
                .WriteTo.Console(outputTemplate: "    [{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
                .MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning))
            .ConfigureServices(services =>
            {
                services.AddNBatch(nbatch =>
                {
                    // Job 1: runs once on startup
                    nbatch.AddJob("startup-import", job => job
                        .AddStep("import", step => step
                            .ReadFrom(new CsvReader<Product>(filePath, row => new Product
                            {
                                Sku = row.GetString("Sku"),
                                Name = row.GetString("Name"),
                                Description = row.GetString("Description"),
                                Price = row.GetDecimal("Price")
                            }))
                            .WriteTo(items =>
                            {
                                Console.WriteLine($"    [startup-import] Wrote {items.Count()} items");
                                return Task.CompletedTask;
                            })
                            .WithChunkSize(10)))
                        .RunOnce();

                    // Job 2: runs every 3 seconds
                    nbatch.AddJob("periodic-check", job => job
                        .AddStep("check", step => step
                            .Execute(() =>
                            {
                                Console.WriteLine($"    [periodic-check] Health check at {DateTime.Now:HH:mm:ss} ?");
                            })))
                        .RunEvery(TimeSpan.FromSeconds(3));
                });
            })
            .Build();

        try
        {
            await host.RunAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Expected — host shut down after 10 seconds
        }

        Console.WriteLine();
        Console.WriteLine("  Host shut down gracefully.");
    }
}
