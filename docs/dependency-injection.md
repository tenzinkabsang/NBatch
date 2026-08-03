---
layout: default
title: DI & Hosted Service
nav_order: 6
---

# Dependency Injection & Hosted Service

NBatch integrates natively with `Microsoft.Extensions.DependencyInjection`. Register your jobs once, then run them **on-demand** via `IJobRunner` or **automatically** as background workers.

---

## Getting Started

```csharp
using NBatch.Core;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddNBatch(nbatch =>
{
    nbatch.AddJob("csv-import", job => job
        .AddStep("import", step => step
            .ReadFrom(new CsvReader<Product>("products.csv", mapFn))
            .WriteTo(new DbWriter<Product>(dbContext))
            .WithChunkSize(100)));
});

var app = builder.Build();
app.Run();
```

---

## `AddNBatch()`

The `AddNBatch()` extension method on `IServiceCollection` is your single entry point. It:

1. Accepts an `Action<NBatchBuilder>` delegate where you register jobs.
2. Registers `IJobRunner` as a **singleton** — inject it anywhere to run jobs on demand.
3. For each scheduled job (`RunOnce()` or `RunEvery()`), registers a dedicated `IHostedService` background worker.

```csharp
builder.Services.AddNBatch(nbatch =>
{
    // Register jobs here
});
```

---

## Registering Jobs

### Simple Job (No DI Dependencies)

```csharp
nbatch.AddJob("csv-import", job => job
    .AddStep("import", step => step
        .ReadFrom(new CsvReader<Product>("products.csv", mapFn))
        .WriteTo(new DbWriter<Product>(dbContext))
        .WithChunkSize(100)));
```

### Job with DI Services

Use the `(IServiceProvider, JobBuilder)` overload to resolve services from the container. NBatch creates a **new DI scope per job run**, so scoped services like `DbContext` work correctly:

```csharp
nbatch.AddJob("csv-import", (sp, job) => job
    .UseJobStore(connStr)                                            // optional
    .WithLogger(sp.GetRequiredService<ILoggerFactory>()
        .CreateLogger("CsvImport"))
    .AddStep("import", step => step
        .ReadFrom(new CsvReader<Product>("products.csv", mapFn))
        .WriteTo(new DbWriter<Product>(sp.GetRequiredService<AppDbContext>()))
        .WithChunkSize(100)));
```

> **Scoped services:** Each call to `IJobRunner.RunAsync()` creates a new `IServiceScope`. This means every run gets its own `DbContext` instance, and it's disposed after the run completes.

---

## Scheduling

Every `AddJob()` call returns a `JobRegistration` that you can optionally schedule:

### `RunOnce()` — Execute Once at Startup

```csharp
nbatch.AddJob("seed-database", job => job
    .AddStep("seed", step => step
        .Execute(async () =>
        {
            await dbContext.Database.MigrateAsync();
            await SeedDefaultDataAsync(dbContext);
        })))
    .RunOnce();
```

The background worker runs the job once when the host starts, then exits. The job remains available on-demand via `IJobRunner`.

### `RunEvery()` — Recurring Background Job

```csharp
nbatch.AddJob("hourly-sync", (sp, job) => job
    .AddStep("sync", step => step
        .ReadFrom(new DbReader<Order>(
            sp.GetRequiredService<AppDbContext>(),
            q => q.Where(o => o.Status == "new").OrderBy(o => o.Id)))
        .WriteTo(new FlatFileItemWriter<Order>("orders.csv"))
        .WithChunkSize(200)))
    .RunEvery(TimeSpan.FromHours(1));
```

The interval is measured from the **completion** of each run, so runs never overlap. If a run fails, the error is logged and the next run starts after the interval.

By default the first run starts immediately at startup; pass `runImmediately: false` to wait one interval first:

```csharp
.RunEvery(TimeSpan.FromHours(1), runImmediately: false)
```

When combined with [`UseJobStore(...)`](job-store), each successful run auto-resets the job, so the next interval reprocesses from the beginning; a failed run resumes where it left off.

> Job names must be unique — registering the same name twice throws `ArgumentException`.

### `RunOnCron()` — Cron Schedules

For calendar-based schedules (nightly at 02:00, weekdays at 06:30), use a standard
5-field cron expression:

```csharp
nbatch.AddJob("nightly-import", (sp, job) => job
    .AddStep("import", step => step
        .ReadFrom(new CsvReader<Product>("data.csv"))
        .WriteTo<ProductWriter>()))
    .RunOnCron("0 2 * * *");                          // 02:00 every day, UTC

// Evaluate the schedule in a specific time zone:
    .RunOnCron("30 6 * * 1-5", TimeZoneInfo.FindSystemTimeZoneById("America/Chicago"));
```

- Standard 5-field format (minute granularity), parsed and validated at registration
  by [Cronos](https://github.com/HangfireIO/Cronos) — an invalid expression throws
  `ArgumentException` immediately.
- The schedule is evaluated in **UTC** unless a `TimeZoneInfo` is provided.
- **Skip-if-missed**: occurrences that pass while a run is still in progress are
  skipped; the next run is always computed from the current time.
- The last schedule call wins: `RunOnce()`, `RunEvery(...)`, and `RunOnCron(...)`
  each replace any previously configured schedule.

---

## Resolving Components from DI

Instead of constructing readers, processors, and writers inline, resolve them from
the container with the type-based builder overloads:

```csharp
services.AddScoped<OrderReader>();       // registration is optional — see below
services.AddScoped<OrderEnricher>();
services.AddScoped<OrderWriter>();

services.AddNBatch(nbatch =>
{
    nbatch.AddJob("orders", job => job
        .AddStep("etl", step => step
            .ReadFrom<OrderReader, Order>()
            .ProcessWith<OrderEnricher, EnrichedOrder>()
            .WriteTo<OrderWriter>()));

    nbatch.AddJob("cleanup", job => job
        .AddStep("purge", step => step.Execute<PurgeTempFilesTasklet>()));
});
```

- A **registered** service is resolved from the container; an **unregistered** type is
  constructed via `ActivatorUtilities` with its constructor dependencies supplied by DI.
- Components are resolved **fresh for every run**, inside the run's DI scope — scoped
  services like a `DbContext` behave exactly as they would in a scoped request.
- These overloads are only available for jobs registered via `AddNBatch(...)`;
  standalone `Job.CreateBuilder(...)` jobs get a clear `InvalidOperationException`.

---

## Job-Level Defaults

Set chunk size, skip policy, or retry policy once per job instead of per step:

```csharp
nbatch.AddJob("etl", job => job
    .WithDefaultChunkSize(500)
    .WithDefaultSkipPolicy(SkipPolicy.For<FormatException>(maxSkips: 10))
    .WithDefaultRetryPolicy(RetryPolicy.For<TimeoutException>(maxAttempts: 3))
    .AddStep("extract", step => step.ReadFrom(r1).WriteTo(w1))
    .AddStep("load", step => step
        .ReadFrom(r2)
        .WriteTo(w2)
        .WithChunkSize(50)));   // per-step settings override the defaults
```

Declaration order doesn't matter — defaults declared after `AddStep` still apply.

### On-Demand Only (No Schedule)

If you don't call `RunOnce()` or `RunEvery()`, the job is registered but **no background worker is created**. Trigger it manually:

```csharp
nbatch.AddJob("export-orders", (sp, job) => job
    .AddStep("export", step => step
        .ReadFrom(new DbReader<Order>(
            sp.GetRequiredService<AppDbContext>(),
            q => q.OrderBy(o => o.Id)))
        .WriteTo(new FlatFileItemWriter<Order>("orders.csv"))
        .WithChunkSize(200)));
// No .RunOnce() or .RunEvery() — on-demand only
```

---

## `IJobRunner` — On-Demand Execution

Inject `IJobRunner` anywhere in your application to trigger jobs programmatically:

```csharp
public interface IJobRunner
{
    Task<JobResult> RunAsync(string jobName, CancellationToken cancellationToken = default);
}
```

### From a Minimal API Endpoint

```csharp
app.MapPost("/jobs/{name}/run", async (string name, IJobRunner runner, CancellationToken ct) =>
{
    var result = await runner.RunAsync(name, ct);
    return result.Success ? Results.Ok(result) : Results.StatusCode(500);
});
```

### From an MVC Controller

```csharp
[ApiController]
[Route("api/[controller]")]
public class BatchController(IJobRunner jobRunner) : ControllerBase
{
    [HttpPost("{jobName}/run")]
    public async Task<IActionResult> RunJob(string jobName, CancellationToken ct)
    {
        var result = await jobRunner.RunAsync(jobName, ct);
        return result.Success ? Ok(result) : StatusCode(500, result);
    }
}
```

### From Another Service

```csharp
public class OrderService(IJobRunner jobRunner)
{
    public async Task ProcessNewOrdersAsync(CancellationToken ct)
    {
        // ... business logic ...
        await jobRunner.RunAsync("process-orders", ct);
    }
}
```

---

## Multiple Jobs

Register as many jobs as you need. Each scheduled job gets its own independent background worker:

```csharp
builder.Services.AddNBatch(nbatch =>
{
    // Runs every 30 minutes
    nbatch.AddJob("import-products", (sp, job) => job
        .AddStep("import", step => step
            .ReadFrom(new CsvReader<Product>("products.csv", mapFn))
            .WriteTo(new DbWriter<Product>(sp.GetRequiredService<AppDbContext>()))
            .WithChunkSize(100)))
        .RunEvery(TimeSpan.FromMinutes(30));

    // Runs once at startup
    nbatch.AddJob("warmup-cache", job => job
        .AddStep("warmup", step => step
            .Execute(() => CacheService.WarmUpAsync())))
        .RunOnce();

    // On-demand only — triggered via IJobRunner
    nbatch.AddJob("export-orders", (sp, job) => job
        .AddStep("export", step => step
            .ReadFrom(new DbReader<Order>(
                sp.GetRequiredService<AppDbContext>(),
                q => q.OrderBy(o => o.Id)))
            .WriteTo(new FlatFileItemWriter<Order>("orders.csv"))
            .WithChunkSize(200)));
});
```

---

## Complete Example

A full ASP.NET Core application with NBatch:

```csharp
using NBatch.Core;

var builder = WebApplication.CreateBuilder(args);

// Register your EF Core DbContext
builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

// Register NBatch jobs
builder.Services.AddNBatch(nbatch =>
{
    var connStr = builder.Configuration.GetConnectionString("Default")!;

    // Background job: import CSV every hour
    nbatch.AddJob("csv-import", (sp, job) => job
        .UseJobStore(connStr)
        .WithLogger(sp.GetRequiredService<ILoggerFactory>().CreateLogger("CsvImport"))
        .AddStep("import", step => step
            .ReadFrom(new CsvReader<Product>("products.csv", row => new Product
            {
                Name  = row.GetString("Name"),
                Price = row.GetDecimal("Price")
            }))
            .WriteTo(new DbWriter<Product>(sp.GetRequiredService<AppDbContext>()))
            .WithSkipPolicy(SkipPolicy.For<FlatFileParseException>(maxSkips: 5))
            .WithChunkSize(100))
        .AddStep("notify", step => step
            .Execute(() => Console.WriteLine("Import complete!"))))
        .RunEvery(TimeSpan.FromHours(1));

    // On-demand job: export
    nbatch.AddJob("export-products", (sp, job) => job
        .AddStep("export", step => step
            .ReadFrom(new DbReader<Product>(
                sp.GetRequiredService<AppDbContext>(),
                q => q.OrderBy(p => p.Id)))
            .WriteTo(new FlatFileItemWriter<Product>("products-export.csv").WithDelimiter(','))
            .WithChunkSize(200)));
});

var app = builder.Build();

// Expose job trigger endpoint
app.MapPost("/jobs/{name}/run", async (string name, IJobRunner runner, CancellationToken ct) =>
{
    var result = await runner.RunAsync(name, ct);
    return result.Success ? Results.Ok(result) : Results.StatusCode(500);
});

app.Run();
```

---

## How It Works Under the Hood

1. `AddNBatch()` builds a `NBatchBuilder` containing job factories (name → `Func<IServiceProvider, Job>`).
2. A singleton `JobRunner` is registered. It holds all factories and creates a new DI scope per `RunAsync()` call.
3. For each `RunOnce()` or `RunEvery()` registration, an `NBatchJobWorkerService` (a `BackgroundService`) is registered as an `IHostedService`.
4. Workers yield immediately on startup to avoid blocking other hosted services, then begin their schedule.
5. If a scheduled job throws (non-cancellation), the error is logged and the next run starts after the interval. The worker never crashes.

---

**Next:** [Listeners &rarr;](listeners)
