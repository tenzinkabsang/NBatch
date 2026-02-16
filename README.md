# NBatch

[![NuGet Version](https://img.shields.io/nuget/v/NBatch.svg?style=flat)](https://www.nuget.org/packages/NBatch/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/NBatch.svg?style=flat)](https://www.nuget.org/packages/NBatch/)
[![CI](https://github.com/tenzinkabsang/NBatch/actions/workflows/ci.yml/badge.svg)](https://github.com/tenzinkabsang/NBatch/actions/workflows/ci.yml)

**A lightweight batch processing framework for .NET, inspired by Spring Batch.**

📖 **[Full Documentation](https://tenzinkabsang.github.io/NBatch/)** — guides, API reference, and examples.

NBatch gives you a declarative, step-based pipeline for ETL jobs, data migrations, and scheduled tasks. Wire up readers, processors, and writers — NBatch handles chunking, error skipping, progress tracking, and restart-from-failure.

## Packages

| Package | Description |
|---------|-------------|
| [`NBatch`](https://www.nuget.org/packages/NBatch/) | Core framework — interfaces, chunking, skip policies, `CsvReader`, `DbReader`/`DbWriter`, DI, hosted service |
| [`NBatch.EntityFrameworkCore`](https://www.nuget.org/packages/NBatch.EntityFrameworkCore/) | EF Core job store for restart-from-failure (SQL Server, PostgreSQL, SQLite, MySQL/MariaDB) |

```bash
dotnet add package NBatch
dotnet add package NBatch.EntityFrameworkCore   # only if you need persistent job tracking
```

## Examples

```csharp
var job = Job.CreateBuilder("ETL")
    .AddStep("extract-transform", step => step
        .ReadFrom(new CsvReader<Order>(...))
        .WriteTo(new DbWriter<Order>(...))
        .WithChunkSize(100))
    .AddStep("notify", step => step
        .Execute(() => SendEmail()))
    .Build();
```

### With SQL-backed job store for restart-from-failure

```csharp
var job = Job.CreateBuilder("csv-import")
    .UseJobStore(connStr, DatabaseProvider.SqlServer)   // optional — enables restart-from-failure
    .AddStep("import", step => step
        .ReadFrom(new CsvReader<Product>("products.csv", row => new Product
        {
            Name  = row.GetString("Name"),
            Price = row.GetDecimal("Price")
        }))
        .ProcessWith(p => new Product { Name = p.Name.ToUpper(), Price = p.Price })
        .WriteTo(new DbWriter<Product>(dbContext))
        .WithSkipPolicy(SkipPolicy.For<FormatException>(maxSkips: 5))
        .WithChunkSize(100))
    .AddStep("notify", step => step
        .Execute(() => SendEmailAsync()))
    .Build();

await job.RunAsync();
```

## Highlights

- **Chunk-oriented processing** — read, transform, and write in configurable batches
- **Skip policies** — skip malformed records instead of aborting the job
- **Restart from failure** — SQL-backed job store resumes where a crashed job left off
- **Tasklet steps** — fire-and-forget work (send an email, call an API, run a stored proc)
- **Lambda-friendly** — processors and writers can be plain lambdas; no extra classes needed
- **DI & hosted service** — `AddNBatch()`, `RunOnce()`, `RunEvery()` for background jobs
- **Multi-target** — .NET 8, .NET 9, and .NET 10
- **Provider-agnostic** — SQL Server, PostgreSQL, SQLite, or MySQL for the job store; any EF Core provider for your data

## Documentation

See the **[full documentation](https://tenzinkabsang.github.io/NBatch/)** for guides, API reference, and examples:

- [Core concepts](https://tenzinkabsang.github.io/NBatch/core-concepts) — jobs, steps, readers, writers, processors
- [Readers & writers](https://tenzinkabsang.github.io/NBatch/readers-writers) — `CsvReader`, `DbReader`, `DbWriter`, `FlatFileItemWriter`
- [Skip policies](https://tenzinkabsang.github.io/NBatch/skip-policies) — error handling and skip limits
- [Job store](https://tenzinkabsang.github.io/NBatch/job-store) — persistent tracking and restart-from-failure
- [DI & hosted service](https://tenzinkabsang.github.io/NBatch/dependency-injection) — `AddNBatch()`, `IJobRunner`, `RunOnce()`, `RunEvery()`
- [Listeners](https://tenzinkabsang.github.io/NBatch/listeners) — job and step lifecycle hooks
- [API reference](https://tenzinkabsang.github.io/NBatch/api-reference) — all public types and methods
- [Examples](https://tenzinkabsang.github.io/NBatch/examples) — CSV-to-DB, DB-to-file, multi-step, DI, hosted service

## Running locally

```bash
# Start the test database (SQL Server via Docker)
cd src && docker compose up -d

# Build & run the demo console app
dotnet build
dotnet run --project NBatch.ConsoleApp

# Run tests
dotnet test
```

## Contributing

1. Fork the repo
2. Create a feature branch: `git checkout -b my-feature`
3. Commit your changes: `git commit -m "Add my feature"`
4. Push: `git push origin my-feature`
5. Open a pull request

## License

See [LICENSE](LICENSE) for details.
