using Microsoft.Extensions.Logging;
using NBatch.Core;

namespace NBatch.ConsoleApp.Demos;

/// <summary>
/// DEMO 4 — Tasklet Steps
///
/// Shows fire-and-forget units of work that don't follow the
/// reader ? processor ? writer pattern. Two steps:
///   Step 1: synchronous Action tasklet (cleanup)
///   Step 2: async Func&lt;Task&gt; tasklet (simulated API call)
///
/// Features: Execute(Action), Execute(Func&lt;Task&gt;)
/// </summary>
public static class Demo04_TaskletSteps
{
    public static async Task RunAsync(string targetFilePath, ILogger logger)
    {
        var job = Job.CreateBuilder("demo-04-tasklets")
            .WithLogger(logger)
            .AddStep("cleanup", step => step
                .Execute(() =>
                {
                    Console.WriteLine("    [Tasklet] Cleaning up old output file...");
                    if (File.Exists(targetFilePath))
                    {
                        File.Delete(targetFilePath);
                        Console.WriteLine($"    [Tasklet] Deleted: {Path.GetFileName(targetFilePath)}");
                    }
                    else
                    {
                        Console.WriteLine("    [Tasklet] No file to clean up.");
                    }
                }))
            .AddStep("notify", step => step
                .Execute(async ct =>
                {
                    Console.WriteLine("    [Tasklet] Sending completion notification...");
                    await Task.Delay(500, ct); // simulate API call
                    Console.WriteLine("    [Tasklet] Notification sent ?");
                }))
            .Build();

        var result = await job.RunAsync();

        Console.WriteLine();
        Console.WriteLine($"  Job '{result.Name}' — Success: {result.Success}, Steps: {result.Steps.Count}");
    }
}
