---
layout: default
title: Job Store
nav_order: 5
---

# Job Store

The **job store** gives NBatch **restart-from-failure** capability. It tracks which chunks have been processed, so if a job crashes mid-way, the next run resumes where it left off instead of reprocessing everything.

The job store lives in a separate package &mdash; install it alongside the core:

```bash
dotnet add package NBatch.EntityFrameworkCore
```

---

## Enabling the Job Store

```csharp
var job = Job.CreateBuilder("csv-import")
    .UseJobStore(connectionString)                           // SQL Server (default)
    .AddStep("import", step => step
        .ReadFrom(reader)
        .WriteTo(writer)
        .WithChunkSize(100))
    .Build();
```

NBatch will automatically create the required tracking tables (`nbatch.jobs`, `nbatch.steps`, `nbatch.step_exceptions`) if they don't exist.

---

## Supported Providers

```csharp
// SQL Server (default)
.UseJobStore(connStr, DatabaseProvider.SqlServer)

// PostgreSQL
.UseJobStore(connStr, DatabaseProvider.PostgreSql)

// SQLite
.UseJobStore(connStr, DatabaseProvider.Sqlite)

// MySQL / MariaDB (.NET 8 & 9 only)
.UseJobStore(connStr, DatabaseProvider.MySql)
```

The `DatabaseProvider` enum:

| Value | Provider | Notes |
|-------|----------|-------|
| `SqlServer` | Microsoft SQL Server | Default |
| `PostgreSql` | PostgreSQL via Npgsql | |
| `Sqlite` | SQLite | |
| `MySql` | MySQL / MariaDB via Pomelo | .NET 8 &amp; .NET 9 only |

> **Note:** MySQL support uses the [Pomelo.EntityFrameworkCore.MySql](https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql) provider, which does not yet support .NET 10. NBatch will throw `PlatformNotSupportedException` if you select `DatabaseProvider.MySql` on .NET 10.

---

## How It Works

1. When a job starts, NBatch creates a **job record** in the tracking database.
2. Before each chunk is processed, NBatch inserts a **step record** with the current chunk index.
3. After each chunk completes, the step record is updated with success/failure status.
4. On the next run, NBatch queries the last successful chunk index and **resumes from there**.

### Restart Flow

```
Run 1:  Chunk 0 [ok] -> Chunk 1 [ok] -> Chunk 2 [ok] -> Chunk 3 [CRASH]
Run 2:  Resumes from Chunk 3 -> Chunk 3 [ok] -> Chunk 4 [ok] -> Done!
```

### Schema

All tracking tables are created under the `nbatch` schema:

| Table | Purpose |
|-------|---------|
| `nbatch.jobs` | One row per job &mdash; name, creation date, last run |
| `nbatch.steps` | One row per chunk processed &mdash; step index, items count, errors |
| `nbatch.step_exceptions` | One row per skipped exception &mdash; type, message, stack trace |

---

## With Dependency Injection

When using `AddNBatch()`, you can configure the job store inside the job builder:

```csharp
builder.Services.AddNBatch(nbatch =>
{
    nbatch.AddJob("csv-import", job => job
        .UseJobStore(connStr, DatabaseProvider.PostgreSql)
        .AddStep("import", step => step
            .ReadFrom(reader)
            .WriteTo(writer)
            .WithChunkSize(100)))
        .RunEvery(TimeSpan.FromHours(1));
});
```

---

## In-Memory Mode (Default)

If you **don't** call `.UseJobStore(...)`, NBatch uses an in-memory repository. This is lightweight and suitable for:

- One-off scripts
- Development and testing
- Jobs where reprocessing is acceptable

```csharp
// No .UseJobStore() -- runs with in-memory tracking
var job = Job.CreateBuilder("simple-job")
    .AddStep("work", step => step
        .ReadFrom(reader)
        .WriteTo(writer))
    .Build();
```

> **Note:** In-memory mode does not persist state between runs. Restarting the application will reprocess all data.

---

## Resetting the Job Store

If you need to reprocess data from scratch, reset the tracking tables:

```sql
-- Clear all tracking data
DELETE FROM nbatch.step_exceptions;
DELETE FROM nbatch.steps;
DELETE FROM nbatch.jobs;
```

Or drop and recreate the database if using Docker:

```bash
docker compose down -v
docker compose up -d
```

---

**Next:** [DI & Hosted Service &rarr;](dependency-injection)
