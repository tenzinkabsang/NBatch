# Changelog

All notable changes to NBatch will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/), and this project adheres to [Semantic Versioning](https://semver.org/).

---

## [2.0.0] — Unreleased

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
