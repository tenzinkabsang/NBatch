---
layout: default
title: Home
nav_order: 1
permalink: /
---

<p align="center">
  <strong>Declarative, step-based pipelines for ETL jobs, data migrations, and scheduled tasks.</strong>
</p>

<p align="center">
  <a href="https://www.nuget.org/packages/NBatch/">
    <img src="https://img.shields.io/nuget/v/NBatch.svg?style=flat-square" alt="NuGet Version">
  </a>
  <a href="https://www.nuget.org/packages/NBatch/">
    <img src="https://img.shields.io/nuget/dt/NBatch.svg?style=flat-square" alt="NuGet Downloads">
  </a>
  <a href="https://github.com/tenzinkabsang/NBatch/blob/main/LICENSE">
    <img src="https://img.shields.io/github/license/tenzinkabsang/NBatch?style=flat-square" alt="License">
  </a>
</p>

---

## Why NBatch?

Wire up **readers**, **processors**, and **writers** � NBatch handles chunking, error skipping, progress tracking, and restart-from-failure so you can focus on your business logic.

### ? Highlights

| Feature | Description |
|---------|-------------|
| **Chunk-oriented processing** | Read, transform, and write data in configurable batches |
| **Skip policies** | Keep the job running when a record is malformed; skip it and move on |
| **Restart from failure** | SQL-backed job store tracks progress so a crashed job resumes where it left off |
| **Tasklet steps** | Fire-and-forget units of work (send an email, call an API, run a stored proc) |
| **Lambda-friendly** | Processors and writers can be plain lambdas; no extra classes required |
| **Multi-target** | Supports **.NET 8**, **.NET 9**, and **.NET 10** |
| **Provider-agnostic** | SQL Server, PostgreSQL, or SQLite for the job store; any EF Core provider for your data |

---

## Quick Start

### Install

```bash
dotnet add package NBatch
```

### Your First Job

```csharp
var job = Job.CreateBuilder("csv-to-db")
    .UseJobStore(connStr)
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

---

## ?? Documentation

| Page | Description |
|------|-------------|
| [Core Concepts](core-concepts) | Jobs, steps, chunk processing, and the pipeline model |
| [Readers & Writers](readers-writers) | Built-in components for CSV, database, and flat-file I/O |
| [Skip Policies](skip-policies) | Error tolerance and skip-limit configuration |
| [Job Store](job-store) | SQL-backed progress tracking and restart-from-failure |
| [Listeners](listeners) | Job and step lifecycle hooks for logging and monitoring |
| [API Reference](api-reference) | Interfaces, builders, and result types |
| [Examples](examples) | Real-world usage patterns and recipes |

---

## More Examples

### Database ? File

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

### Multi-step with Tasklet

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

### Lambda-Only (No Extra Classes)

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

## Contributing

1. Fork the repo
2. Create a feature branch: `git checkout -b my-feature`
3. Commit your changes: `git commit -m "Add my feature"`
4. Push: `git push origin my-feature`
5. Open a pull request

---

## License

[MIT](https://github.com/tenzinkabsang/NBatch/blob/main/LICENSE) � Copyright � Tenzin Kabsang
