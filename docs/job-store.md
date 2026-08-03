---
layout: default
title: Job Store
nav_order: 5
---

# Job Store

The **job store** gives NBatch **restart-from-failure** capability. It tracks which chunks have been processed, so if a job crashes mid-way, the next run resumes where it left off instead of reprocessing everything. After a **successful** run the job automatically resets, so the next run starts fresh — a scheduled job reprocesses on every interval.

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

1. When a job starts, NBatch creates or updates a **job record** in the tracking database and marks the run as in-flight.
2. Before each chunk is processed, NBatch inserts a **step record** for the chunk, marked **in-flight (presumed failed)**.
3. After each chunk completes, the step record is updated with the real outcome. A chunk whose record was never updated — a process kill or power loss mid-chunk — therefore reads as *failed*, never as *complete*: an interrupted chunk is always re-processed, and records can't be silently lost.
4. When the run finishes, the outcome is recorded on the job record.
5. On the next run, that outcome decides between **reset** and **resume**. The resume position is always the **last committed chunk boundary**, independent of the chunk size the failed run used — you can safely restart with a different `ChunkSize`.

| Previous run | Next run |
|--------------|----------|
| Completed successfully | **Starts fresh** — step progress resets to the beginning, tasklets run again |
| Failed | Resumes at the failed chunk's start and retries it; tasklets that already completed are skipped |
| Crashed mid-run | Resumes at the interrupted chunk's start — the in-flight record reads as failed |
| Cancelled | Resumes; the chunk that was aborted is retried |

### Restart Flow

```
Run 1:  Chunk 0 [ok] -> Chunk 1 [ok] -> Chunk 2 [ok] -> Chunk 3 [CRASH]
Run 2:  Resumes from Chunk 3 -> Chunk 3 [ok] -> Chunk 4 [ok] -> Done!
Run 3:  Previous run succeeded -> starts fresh from Chunk 0
```

Because a failed run may retry a partially processed chunk, delivery is **at-least-once**:
writers should be idempotent or tolerate re-written items.

> **Note:** Running the same job concurrently (two hosts, or a manual run overlapping a
> scheduled one) is not supported — both runs would read and advance the same progress.

### Schema

All tracking tables are created under the `nbatch` schema:

| Table | Purpose |
|-------|---------|
| `nbatch.jobs` | One row per job &mdash; name, creation date, last run, last run outcome |
| `nbatch.steps` | One row per chunk processed &mdash; step index, items count, errors |
| `nbatch.step_exceptions` | One row per skipped item &mdash; item index, message, stack trace |

### Upgrading from NBatch 2.x

Version 3 adds a `last_run_success` column to `nbatch.jobs`. NBatch adds it automatically
(and idempotently) the first time it touches an existing 2.x database. If your database user
cannot run DDL, apply it manually — see the [CHANGELOG](https://github.com/tenzinkabsang/NBatch/blob/main/CHANGELOG.md)
for the per-provider `ALTER TABLE` statements. The first v3 run against a v2 database
resumes (old behavior); auto-reset applies from the next completed run onward.

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

A successful run resets automatically, so manual resets are rarely needed. To force a
**failed** job to start from scratch instead of resuming (e.g. after fixing bad source
data), clear the tracking tables:

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
