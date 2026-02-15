---
layout: default
title: Examples
nav_order: 8
---

# Examples

Real-world usage patterns for common batch processing scenarios.

---

## CSV ? Database

Import a CSV file into a database with error tolerance and progress tracking.

```csharp
var job = Job.CreateBuilder("csv-to-db")
    .UseJobStore(connStr)
    .AddStep("import", step => step
        .ReadFrom(new CsvReader<Product>("products.csv", row => new Product
        {
            Name        = row.GetString("Name"),
            Description = row.GetString("Description"),
            Price       = row.GetDecimal("Price")
        }))
        .WriteTo(new DbWriter<Product>(dbContext))
        .WithSkipPolicy(SkipPolicy.For<FlatFileParseException>(maxSkips: 3))
        .WithChunkSize(100))
    .Build();

await job.RunAsync();
```

---

## Database ? File

Export database records to a flat file.

```csharp
var job = Job.CreateBuilder("db-to-file")
    .UseJobStore(connStr)
    .AddStep("export", step => step
        .ReadFrom(new DbReader<Product>(dbContext, q => q.OrderBy(p => p.Id)))
        .WriteTo(new FlatFileItemWriter<Product>("output.csv").WithToken(','))
        .WithChunkSize(50))
    .Build();

await job.RunAsync();
```

---

## ETL with Transformation

Read from one source, transform the data, and write to another.

```csharp
var job = Job.CreateBuilder("etl-orders")
    .UseJobStore(connStr)
    .AddStep("extract-transform", step => step
        .ReadFrom(new DbReader<Order>(sourceDb, q => q.OrderBy(o => o.Id)))
        .ProcessWith(o => new OrderDto
        {
            Id    = o.Id,
            Total = o.Total,
            Date  = o.CreatedAt.ToString("yyyy-MM-dd")
        })
        .WriteTo(new FlatFileItemWriter<OrderDto>("orders.csv"))
        .WithChunkSize(100))
    .Build();

await job.RunAsync();
```

---

## Multi-Step with Tasklet

Combine chunk-oriented steps with fire-and-forget tasklets.

```csharp
var job = Job.CreateBuilder("full-pipeline")
    .UseJobStore(connStr)
    .AddStep("extract", step => step
        .ReadFrom(new CsvReader<Product>("data.csv", mapFn))
        .WriteTo(new DbWriter<Product>(dbContext))
        .WithChunkSize(200))
    .AddStep("validate", step => step
        .Execute(async () =>
        {
            var count = await dbContext.Products.CountAsync();
            if (count == 0) throw new Exception("No products imported!");
        }))
    .AddStep("notify", step => step
        .Execute(() => emailService.SendAsync("Import complete!")))
    .Build();

await job.RunAsync();
```

---

## Lambda-Only Pipeline

No custom classes needed -- everything is inline.

```csharp
var job = Job.CreateBuilder("quick-job")
    .AddStep("transform", step => step
        .ReadFrom(new CsvReader<Product>("data.csv", row => new Product
        {
            Name  = row.GetString("Name"),
            Price = row.GetDecimal("Price")
        }))
        .ProcessWith(p => new Product
        {
            Name  = p.Name.ToUpper(),
            Price = p.Price
        })
        .WriteTo(async items =>
        {
            foreach (var item in items)
                Console.WriteLine($"{item.Name}: {item.Price:C}");
        }))
    .Build();

await job.RunAsync();
```

---

## TSV File with Custom Delimiter

Read tab-separated values.

```csharp
var reader = new CsvReader<LogEntry>("access.tsv", row => new LogEntry
{
    Timestamp = row.GetDateTime("time"),
    Url       = row.GetString("url"),
    Status    = row.GetInt("status")
}).WithDelimiter('\t');
```

---

## With Logging

Attach an `ILogger` for diagnostic output.

```csharp
using var loggerFactory = LoggerFactory.Create(b => b
    .AddConsole()
    .SetMinimumLevel(LogLevel.Information));

var logger = loggerFactory.CreateLogger<Program>();

var job = Job.CreateBuilder("logged-job")
    .WithLogger(logger)
    .AddStep("work", step => step
        .ReadFrom(reader)
        .WriteTo(writer)
        .WithChunkSize(50))
    .Build();

await job.RunAsync();
```

---

## With Listeners

Monitor job and step lifecycle events.

```csharp
var job = Job.CreateBuilder("monitored-job")
    .WithListener(new TimingListener())
    .AddStep("import", step => step
        .ReadFrom(reader)
        .WriteTo(writer)
        .WithListener(new StepMetricsListener()))
    .Build();

var result = await job.RunAsync();

// Inspect results
Console.WriteLine($"Job: {result.Name}, Success: {result.Success}");
foreach (var step in result.Steps)
{
    Console.WriteLine($"  {step.Name}: Read={step.ItemsRead}, " +
        $"Processed={step.ItemsProcessed}, Skipped={step.ErrorsSkipped}");
}
```

---

## PostgreSQL Job Store

Use PostgreSQL instead of SQL Server for progress tracking.

```csharp
var job = Job.CreateBuilder("pg-job")
    .UseJobStore(pgConnStr, DatabaseProvider.PostgreSql)
    .AddStep("work", step => step
        .ReadFrom(reader)
        .WriteTo(writer))
    .Build();
```

---

## Running Locally with Docker

```bash
# Start the database
docker compose up -d

# Build and run
dotnet build
dotnet run --project NBatch.ConsoleApp

# Run tests
dotnet test
```

> **Tip:** The job store tracks progress across runs. To reprocess data, reset the `BatchJob` and `BatchStep` tables or recreate the Docker container.

---

[&larr; Back to Home](.)
