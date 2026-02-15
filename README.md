# NBatch

[![NuGet Version](https://img.shields.io/nuget/v/NBatch.svg?style=flat)](https://www.nuget.org/packages/NBatch/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/NBatch.svg?style=flat)](https://www.nuget.org/packages/NBatch/)

**A lightweight batch processing framework for .NET — inspired by Spring Batch.**

📖 **[Full Documentation](https://tenzinkabsang.github.io/NBatch/)** — guides, API reference, and examples.

NBatch gives you a declarative, step-based pipeline for ETL jobs, data migrations, and scheduled tasks. You wire up readers, processors, and writers — NBatch handles chunking, error skipping, progress tracking, and restart-from-failure so you can focus on your business logic.

### Highlights

- **Chunk-oriented processing** — read, transform, and write data in configurable batches.
- **Skip policies** — keep the job running when a record is malformed; skip it and move on.
- **Restart from failure** — optional SQL-backed job store tracks progress so a crashed job resumes where it left off.
- **Tasklet steps** — fire-and-forget units of work (send an email, call an API, run a stored proc).
- **Lambda-friendly** — processors and writers can be plain lambdas; no extra classes required.
- **Multi-target** — supports .NET 8, .NET 9, and .NET 10.
- **Provider-agnostic storage** — SQL Server, PostgreSQL, or SQLite for the job store; any EF Core provider for your data.

---

## Quick start

### Install

```bash
dotnet add package NBatch
```
---

## Examples

```csharp
var job = Job.CreateBuilder("ETL")
    .UseJobStore(connStr)
    .AddStep("extract-transform", step => step
        .ReadFrom(new DbReader<Order>(...))
        .WriteTo(new FileWriter<Order>(...))
        .WithChunkSize(100))
    .AddStep("notify", step => step
        .Execute(() => SendEmail()))
    .Build();
```


### CSV → Database (with job store & skip policy)

```csharp
var job = Job.CreateBuilder("csv-to-db")
    .UseJobStore(connStr)                          // enable SQL-backed progress tracking
    .AddStep("import", step => step
        .ReadFrom(new CsvReader<Product>(filePath, row => new Product
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

### Database → File

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

### Multi-step job with a tasklet

```csharp
var job = Job.CreateBuilder("ETL")
    .UseJobStore(connStr)
    .AddStep("extract-transform", step => step
        .ReadFrom(new DbReader<Order>(sourceDb, q => q.OrderBy(o => o.Id)))
        .ProcessWith(o => new OrderDto { Id = o.Id, Total = o.Total })
        .WriteTo(new FlatFileItemWriter<OrderDto>("orders.csv"))
        .WithChunkSize(100))
    .AddStep("notify", step => step
        .Execute(() => SendEmailAsync()))
    .Build();

await job.RunAsync();
```

### Lambda-only (no classes needed)

Every component — processor and writer — can be a simple lambda:

```csharp
var job = Job.CreateBuilder("quick-job")
    .AddStep("transform", step => step
        .ReadFrom(new CsvReader<Product>("data.csv", row => new Product
        {
            Name  = row.GetString("Name"),
            Price = row.GetDecimal("Price")
        }))
        .ProcessWith(p => new Product { Name = p.Name.ToUpper(), Price = p.Price })
        .WriteTo(async items =>
        {
            foreach (var item in items)
                Console.WriteLine(item);
        }))
    .Build();

await job.RunAsync();
```

---

## Core concepts

| Concept | Description |
|---------|-------------|
| **Job** | A named container of one or more steps, executed in order. |
| **Step** | A chunk-oriented pipeline: `IReader<T>` → `IProcessor<TIn, TOut>` → `IWriter<T>`. |
| **Tasklet step** | A single unit of work (via `Execute(...)`) that doesn't follow the reader/writer pattern. |
| **Skip policy** | Tells the step to skip (not abort) when a specific exception type is thrown, up to a configurable limit. |
| **Job store** | Optional SQL-backed tracking. Call `.UseJobStore(connStr)` to enable restart-from-failure. Omit it for lightweight in-memory tracking. |
| **Chunk size** | Number of items read per iteration. Default is `10`. |

### Built-in readers & writers

| Component | Description |
|-----------|-------------|
| `CsvReader<T>` | Reads delimited text files (CSV, TSV, pipe). Parses headers from the first row automatically. |
| `DbReader<T>` | Reads from any EF Core `DbContext` with paginated chunking. |
| `DbWriter<T>` | Writes to any EF Core `DbContext`. |
| `FlatFileItemWriter<T>` | Serializes objects to a delimited text file. |

### Listeners

Implement `IJobListener` or `IStepListener` for cross-cutting concerns like logging, metrics, or notifications:

```csharp
var job = Job.CreateBuilder("monitored-job")
    .WithListener(new MyJobListener())
    .AddStep("work", step => step
        .ReadFrom(reader)
        .WriteTo(writer)
        .WithListener(new MyStepListener()))
    .Build();
```

---

## Running locally

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download) (or later)
- [Docker](https://www.docker.com/) (for the SQL Server test database)

### 1. Start the database

From the `src/` directory:

```bash
docker compose up -d
```

This spins up a SQL Server container and runs the init script automatically. The default connection string in `appsettings.json` points to `localhost:1433`.

### 2. Build & run

```bash
cd src
dotnet build
dotnet run --project NBatch.ConsoleApp
```

### 3. Run tests

```bash
dotnet test
```

> **Tip:** The job store tracks progress across runs. If you want to reprocess the same data, reset the `BatchJob` and `BatchStep` tables (or delete the database and let `docker compose up` recreate it).

---

## Job store providers

```csharp
// SQL Server (default)
.UseJobStore(connStr, DatabaseProvider.SqlServer)

// PostgreSQL
.UseJobStore(connStr, DatabaseProvider.PostgreSql)

// SQLite
.UseJobStore(connStr, DatabaseProvider.Sqlite)
```

---

## Contributing

1. Fork the repo
2. Create a feature branch: `git checkout -b my-feature`
3. Commit your changes: `git commit -m "Add my feature"`
4. Push: `git push origin my-feature`
5. Open a pull request

---

## License

See [LICENSE](LICENSE) for details.
