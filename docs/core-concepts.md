---
layout: default
title: Core Concepts
nav_order: 2
---

# Core Concepts

NBatch is built around a small set of composable primitives. Understanding these concepts will help you design reliable batch jobs.

---

## Packages

NBatch is distributed as two NuGet packages:

| Package | What's inside |
|---------|---------------|
| **`NBatch`** | Core framework &mdash; job builder, step pipeline, readers, writers, skip policies, DI integration, hosted service |
| **`NBatch.EntityFrameworkCore`** | EF Core job store for restart-from-failure &mdash; SQL Server, PostgreSQL, SQLite, MySQL |

Install the core package to get started. Add the EF Core package only if you need persistent job tracking:

```bash
dotnet add package NBatch
dotnet add package NBatch.EntityFrameworkCore   # optional
```

---

## Job

A **Job** is a named container of one or more **steps**, executed in order. You create jobs using the fluent builder API:

```csharp
var job = Job.CreateBuilder("my-job")
    .AddStep("step-1", step => step
        .ReadFrom(reader)
        .WriteTo(writer))
    .AddStep("step-2", step => step
        .ReadFrom(reader2)
        .WriteTo(writer2))
    .Build();

await job.RunAsync();
```

A `Job` returns a [`JobResult`](api-reference#jobresult) containing the aggregate success status and per-step details.

---

## Step (Chunk-Oriented)

A **Step** is a chunk-oriented pipeline that follows the **Reader > Processor > Writer** pattern:

```
+------------+     +--------------+     +------------+
|  Reader    |---->|  Processor   |---->|  Writer    |
| IReader<T> |     | IProcessor   |     | IWriter<T> |
+------------+     +--------------+     +------------+
```

Each iteration:

1. The **Reader** reads a chunk of items (controlled by `ChunkSize`, default `10`).
2. The **Processor** transforms each item (optional -- if omitted, items pass through unchanged).
3. The **Writer** persists the processed chunk to a destination.

This loop repeats until the reader returns no more data.

```csharp
.AddStep("import", step => step
    .ReadFrom(new CsvReader<Product>(filePath, mapFn))
    .ProcessWith(p => new ProductDto(p.Name, p.Price * 1.1m))
    .WriteTo(new DbWriter<ProductDto>(dbContext))
    .WithChunkSize(100))
```

### Chunk Size

The chunk size controls how many items are read per iteration. Larger chunks mean fewer database round-trips but more memory usage.

```csharp
.WithChunkSize(500)  // read 500 items per iteration
```

Default: **10**

---

## Tasklet Step

A **Tasklet** is a single unit of work that doesn't follow the reader/writer pattern. Use it for fire-and-forget tasks like sending emails, calling APIs, or running stored procedures.

```csharp
.AddStep("notify", step => step
    .Execute(() => SendNotificationAsync()))
```

Tasklets support four signatures:

```csharp
// Simple async action
.Execute(() => DoWorkAsync())

// With cancellation token
.Execute(async ct => await DoWorkAsync(ct))

// Synchronous action
.Execute(() => CleanUpTempFiles())

// Implement ITasklet for full control
.Execute(new MyTasklet())
```

The `ITasklet` interface:

```csharp
public interface ITasklet
{
    Task ExecuteAsync(CancellationToken cancellationToken = default);
}
```

---

## The Pipeline Model

A multi-step job executes its steps sequentially. Each step is independent &mdash; a chunk-oriented step can be followed by a tasklet, or vice versa:

```csharp
var job = Job.CreateBuilder("ETL")
    .AddStep("extract", step => step
        .ReadFrom(csvReader)
        .ProcessWith(transformer)
        .WriteTo(dbWriter)
        .WithChunkSize(100))
    .AddStep("validate", step => step
        .Execute(() => RunValidationAsync()))
    .AddStep("notify", step => step
        .Execute(() => SendReportEmailAsync()))
    .Build();
```

---

## Dependency Injection

NBatch integrates with `IServiceCollection` for DI-based applications. Register jobs with `AddNBatch()` and run them on-demand via `IJobRunner` or automatically via background workers.

```csharp
builder.Services.AddNBatch(nbatch =>
{
    nbatch.AddJob("csv-import", (sp, job) => job
        .AddStep("import", step => step
            .ReadFrom(new CsvReader<Product>("data.csv", mapFn))
            .WriteTo(new DbWriter<Product>(sp.GetRequiredService<AppDbContext>()))
            .WithChunkSize(100)))
        .RunEvery(TimeSpan.FromHours(1));
});
```

See [DI & Hosted Service](dependency-injection) for the full guide.

---

## Result Types

### `JobResult`

Returned by `job.RunAsync()`:

| Property | Type | Description |
|----------|------|-------------|
| `Name` | `string` | The job name |
| `Success` | `bool` | `true` if all steps succeeded |
| `Steps` | `IReadOnlyList<StepResult>` | Per-step results |

### `StepResult`

| Property | Type | Description |
|----------|------|-------------|
| `Name` | `string` | The step name |
| `Success` | `bool` | Whether the step completed successfully |
| `ItemsRead` | `int` | Total items read by the reader |
| `ItemsProcessed` | `int` | Total items written successfully |
| `ErrorsSkipped` | `int` | Items skipped via the skip policy |

---

**Next:** [Readers & Writers &rarr;](readers-writers)
