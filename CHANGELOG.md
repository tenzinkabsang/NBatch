# Changelog

All notable changes to NBatch will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/), and this project adheres to [Semantic Versioning](https://semver.org/).

---

## [3.0.0] — Unreleased

### Breaking Changes
- **Renamed result properties** (clean rename, no aliases — mechanical find/replace):
  - `StepResult.ItemsProcessed` → `StepResult.ItemsWritten`
  - `StepResult.ErrorsSkipped` → `StepResult.ItemsSkipped`
  - `FlatFileItemWriter.WithToken(char)` → `WithDelimiter(char)` (matches `CsvReader`)
  - `StepResult` and `JobResult` gain a trailing `Duration` positional parameter (affects exhaustive positional deconstruction only).
- **`CsvRow` accessors now parse with `CultureInfo.InvariantCulture`.** Previously values were parsed with the server's current culture, so the same file could parse differently per machine locale. Files written with culture-specific formats (e.g. `19,99` decimals) need a custom mapping function.
- **`JobBuilder.Build()` throws `InvalidOperationException` when no steps are registered.** For jobs registered via `AddNBatch`, this surfaces on the first run (builds are lazy per run).
- **Skip policies now skip individual items, not whole chunks.**
  When a chunk fails to read, process, or write, NBatch falls back to handling that chunk one item at a time: good items are still written and only the genuinely failing items are skipped. Consequences:
  - `StepResult.ItemsSkipped` counts skipped **items** (previously: skipped chunks).
  - The skip budget (`maxSkips`) is consumed per item.
  - **Processors are re-invoked** for the items of a failed chunk and must be idempotent. Deterministic, item-keyed logic is safe; call-counting or externally side-effecting processors may observe extra invocations.
  - If the skip limit is exhausted mid-chunk, items already written in that chunk stay written; the restart re-processes the chunk (**at-least-once** semantics).
- **Completed jobs auto-reset on the next run.**
  With a persistent job store, a job whose previous run completed successfully now starts from the beginning on its next run (fresh step progress, tasklets re-run). Previous behavior — resuming past the end forever, so reruns processed zero items — is gone. Failed, crashed, or cancelled runs still resume where they left off. This makes `RunEvery(...)` + `UseJobStore(...)` reprocess on each interval as expected.
- **`DbReader<T>` / `DbWriter<T>` moved to the `NBatch.EntityFrameworkCore` package.**
  Namespaces are unchanged (`NBatch.Readers.DbReader`, `NBatch.Writers.DbWriter`) — add the package reference; no code changes needed. The core `NBatch` package no longer depends on `Microsoft.EntityFrameworkCore`.
- **`JobResult` gains a `Cancelled` property** (positional record change — only affects exhaustive deconstruction).
- **`IJobListener.AfterJobAsync` now fires on cancellation** with `JobResult.Cancelled == true` (previously listeners were bypassed). `Job.RunAsync` still throws `OperationCanceledException` afterwards.
- **A missing CSV file now throws `FileNotFoundException`**, not `FlatFileParseException`. File I/O errors are never treated as skippable parse errors.
- **Duplicate `AddJob` names throw `ArgumentException`** (previously the second registration silently overwrote the first but scheduled both).
- **The in-memory repository now resumes after a failed run** when the same `Job` instance is re-run, matching the persistent job-store contract (previously every rerun started from scratch).

### Added
- **Retry policies** — `RetryPolicy.For<TException>(maxAttempts, delay)` with optional exponential backoff (`WithBackoffMultiplier`), configured per step via `.WithRetryPolicy(...)` or per job via `WithDefaultRetryPolicy(...)`. Transient failures are retried **before** the skip policy is consulted, so a retry that succeeds consumes no skip budget. Matching follows the same inheritance + inner-exception rules as skip policies.
- **Cron scheduling** — `JobRegistration.RunOnCron("0 2 * * *", timeZone?)` (standard 5-field expressions via Cronos, UTC default, validated at registration, skip-if-missed). `RunEvery(interval, runImmediately: false)` defers the first run by one interval.
- **DI-resolved step components** — `ReadFrom<TReader, TItem>()`, `ProcessWith<TProcessor, TOutput>()`, `WriteTo<TWriter>()`, and `Execute<TTasklet>()` resolve from the container (registered service, or `ActivatorUtilities` construction), fresh per run inside the run's DI scope.
- **CSV auto-mapping** — `new CsvReader<T>(path)` binds headers to public settable properties case-insensitively (all common primitives + `DateTime`/`DateTimeOffset`/`Guid`/enums and their nullable forms). `CsvRow` gains `GetDateTime`/`GetGuid` and `*OrNull` accessors.
- **`JobResult.EnsureSuccess()`** — throws `JobFailedException` (naming the failed step, carrying the result) instead of relying on callers to check `Success`.
- **Job-level defaults** — `WithDefaultChunkSize`, `WithDefaultSkipPolicy`, `WithDefaultRetryPolicy` on `JobBuilder`, overridable per step, order-independent.
- **OpenTelemetry-ready observability** — `nbatch.job`/`nbatch.step` activities, `nbatch.items.read/written/skipped` counters, and an `nbatch.step.duration` histogram under source/meter `NBatchDiagnostics.SourceName` ("NBatch"); `Duration` on both result records. Zero new dependencies.
- **Item-level scan mode** — the item-at-a-time fallback described above; single bad records no longer discard up to `ChunkSize − 1` good records.
- **Skip policy matching honors inheritance and inner exceptions** — `SkipPolicy.For<IOException>` matches `FileNotFoundException`; a policy for `FormatException` matches a `FlatFileParseException` wrapping one (inner-exception chain walked up to depth 10).
- **RFC 4180 quoted CSV fields** — `CsvReader` handles quoted fields containing the delimiter and `""` escape sequences (embedded newlines are not supported). Duplicate header names are rejected with a clear error.
- **`FlatFileParseException.LineNumber`** — parse and mapping errors carry the 1-based line number of the failing line.
- **Job-completion tracking** — new `last_run_success` column on `nbatch.jobs`, added automatically to existing v2 databases by an idempotent migration on first use (SQL Server, PostgreSQL, SQLite, MySQL).
- **Tasklet completion tracking** — a tasklet that completed successfully is skipped when the job resumes after a later step's failure, and runs again after a successful run resets the job.
- **Order-independent job builder** — `.AddStep(...)` before `.UseJobStore(...)` / `.WithLogger(...)` now works; the repository and logger are bound at `Build()`.
- **Cancelled chunks are recorded** — a chunk aborted by cancellation is marked as an error so the restart backs up and re-processes it (previously its items could be silently lost).
- **Crash-safe chunk tracking** — step records are written as *in-flight (presumed failed)* when a chunk starts and only marked complete when it commits. A process kill or power loss mid-chunk now always re-processes the interrupted chunk on restart; previously the pre-inserted record could read as complete and the chunk's items were silently lost.
- **Exact resume positions** — a restart resumes at the last *committed* chunk boundary, computed from the job store rather than by subtracting the current chunk size. Restarting a failed job with a different `ChunkSize` is now safe (previously a smaller chunk size could permanently skip records).
- **`DbReader` enforces deterministic ordering** — the first read throws `InvalidOperationException` if the query has no `OrderBy`: unordered `Skip`/`Take` pagination returns rows in arbitrary order and silently corrupts both chunking and restarts.
- **Reader positional-contract guard** — the step engine fails loudly if a reader returns a partial chunk and then produces more items (the gap positions would otherwise be silently skipped). `IReader` now documents the full contract.
- **RFC 4180 output escaping** — `FlatFileItemWriter` quotes fields containing the delimiter, a quote, or a line break (embedded quotes doubled) and formats values with the invariant culture, so its output round-trips through `CsvReader` on any machine locale.
- **Composite job-store indexes** — new databases index `steps (job_name, step_name, id)` and `step_exceptions (job_name, step_name, execution_id)` for fast resume lookups on large run histories.
- **Package icon** embedded in both NuGet packages.

### Fixed
- Skip bookkeeping could fail the step it was protecting: exception messages/stack traces longer than the store's column sizes (500/5000 chars) made the insert throw on SQL Server and PostgreSQL. Text is now truncated to fit.
- `CsvReader` treated a run of blank lines longer than the chunk size as end-of-data, silently dropping the rest of the file. Blank lines no longer occupy chunk positions; `FlatFileParseException.LineNumber` still reports the exact physical line.
- `.AddStep(...).UseJobStore(...)` silently binding steps to the in-memory repository and `NullLogger` (steps registered before configuration never persisted progress).
- Step execution order is now guaranteed by an ordered list instead of relying on dictionary insertion-order behavior.
- CSV parse and mapping errors escaping the reader's exception wrapper due to deferred enumeration — the documented `SkipPolicy.For<FlatFileParseException>` pattern now actually matches them.
- `DbWriter` change-tracker growth: written entities are detached after each save, keeping memory flat on large jobs (entities tracked by the caller are untouched).
- `DbReader` silently overflowing on start indexes beyond `int.MaxValue` — now throws `ArgumentOutOfRangeException`.
- Execution-id mismatch on a job's first run (skip-budget scoping used a different timestamp than the one stored).
- Assembly version now matches the package version in published builds (publish workflow passes the tag version to the build step).
- Vulnerable transitive dependency `SQLitePCLRaw.lib.e_sqlite3` 2.1.11 (GHSA-2m69-gcr7-jv3q) pinned to a patched version.
- Mojibake (`—` rendered as a stray byte) across source comments, log messages, and package metadata.

### Migration guide (2.x → 3.0)
1. **If you use `DbReader` or `DbWriter`**: add a reference to `NBatch.EntityFrameworkCore`. No `using` changes needed.
2. **Job-store schema**: the `last_run_success` column is added automatically on first run. In locked-down environments, apply it manually instead:
   - SQL Server: `ALTER TABLE [nbatch].[jobs] ADD [last_run_success] bit NULL;`
   - PostgreSQL: `ALTER TABLE nbatch.jobs ADD COLUMN last_run_success boolean;`
   - SQLite: `ALTER TABLE jobs ADD COLUMN last_run_success INTEGER NULL;`
   - MySQL: `ALTER TABLE jobs ADD COLUMN last_run_success tinyint(1) NULL;`
3. **First v3 run against a v2 database resumes** (the new column is `NULL`, treated as "resume"). Auto-reset takes effect from the run after the first completed v3 run. If you relied on "run once, then never reprocess", guard the rerun externally — that behavior is no longer the default.
4. **Review processors for idempotency**: items in a failed chunk are re-processed during the item-level scan, and restarts after mid-chunk failures may re-write already-written items.
5. **Skip counts are per item** — if you alert on the skip count (now `StepResult.ItemsSkipped`), expect item counts now.
6. **Renames are a mechanical find/replace**:

   | v2 | v3 |
   |----|----|
   | `StepResult.ItemsProcessed` | `StepResult.ItemsWritten` |
   | `StepResult.ErrorsSkipped` | `StepResult.ItemsSkipped` |
   | `FlatFileItemWriter.WithToken(...)` | `FlatFileItemWriter.WithDelimiter(...)` |

7. **CSV parsing is invariant-culture** — if your files use culture-specific number/date formats, parse them explicitly in a mapping function.
8. **Retry runs before skip** — existing skip-only configurations behave identically; add `.WithRetryPolicy(...)` only where transient failures occur.

---

## [2.0.0]

### Breaking Changes
- **Fluent builder API replaces constructor-based configuration.**
  Jobs are now created via `Job.CreateBuilder("name").AddStep(...).Build()`.
- **`IWriter.WriteAsync` returns `Task` instead of `Task<bool>`.**
  Writers now throw on failure instead of returning `false`.
- **`CancellationToken` added to all interfaces.**
  `IReader.ReadAsync`, `IProcessor.ProcessAsync`, `IWriter.WriteAsync`, `ITasklet.ExecuteAsync`, and `Job.RunAsync` all accept a `CancellationToken`.
- **`StepContext` and `SkipContext` are now internal.**
  Steps load their own context from the repository; no public surface change.

### Added
- **Tasklet steps** — fire-and-forget units of work via `Execute(...)`.
- **Lambda-friendly API** — processors and writers can be plain lambdas; no extra classes required.
- **Async processor lambdas** — `.ProcessWith(async (item, ct) => ...)` for async transformations.
- **`CsvReader<T>`** — reads delimited text files with automatic header detection and configurable delimiters.
- **`DbReader<T>` / `DbWriter<T>`** — EF Core-based reader and writer; provider-agnostic.
- **`FlatFileItemWriter<T>`** — writes objects to delimited text files.
- **`SkipPolicy.For<TException>(maxSkips)`** — fluent, type-safe skip policy factory.
- **`UseJobStore(connStr, provider)`** — opt-in SQL-backed progress tracking for restart-from-failure.
- **SQLite support** — `DatabaseProvider.Sqlite` added for lightweight job stores.
- **Multi-target** — supports .NET 8, .NET 9, and .NET 10.
- **Job and step listeners** — `IJobListener` and `IStepListener` for cross-cutting concerns.
- **`ILogger` support** — optional logging via `Microsoft.Extensions.Logging.Abstractions`.
- **Chunk size validation** — `WithChunkSize` rejects zero and negative values.
- **`DelegateWriter` cancellation support** — `WriteTo` lambdas can now receive a `CancellationToken`.
- **`Execute(Action)` overload** — synchronous tasklets without `Task.CompletedTask` boilerplate.

### Fixed
- `PropertyValueSerializer.Serialize` crash on empty collections.
- `SkipContext.ExceptionDetail` null reference when `Exception.StackTrace` is null.
- `DelegateWriter` silently ignoring `CancellationToken`.
- `InMemoryJobRepository` thread safety (switched to `ConcurrentDictionary` / `Interlocked`).

---

## [1.0.0] — Previous release

Initial release of NBatch.
