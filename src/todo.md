# NBatch v2.0.0 — TODO

## P0 — Must fix before release

- [x] **Add `CancellationToken` to all interfaces**
  - `IReader.ReadAsync`, `IProcessor.ProcessAsync`, `IWriter.WriteAsync`, `ITasklet.ExecuteAsync`, `Job.RunAsync`
  - Adding later is a breaking change to every interface
  - Thread through `Step.ProcessChunkAsync`, `EfJobRepository`, listeners

- [x] **Integration tests for restart-from-failure**
  - Job restart from a failed chunk (the headline feature)
  - `StepContext.RetryPreviousIfFailed` with various offsets
  - `TaskletStep` error handling
  - `EfJobRepository` with SQLite provider (`DatabaseProvider.Sqlite` added)

- [x] **Extract `.UseJobStore()` from `CreateBuilder`**
  - Removed `CreateBuilder(name, connStr, provider)` overload
  - Added `JobBuilder.UseJobStore(connectionString, provider)` fluent method
  - In-memory tracking is now the default; SQL-based tracking is opt-in
  - Makes the job-tracking DB purpose obvious to new users

- [x] **Eliminate inner `.Build()` on `FlatFileItemBuilder`**
  - `FlatFileItemBuilder<T>` now implements `IReader<T>` directly
  - Users pass the builder straight to `.ReadFrom()` without calling `.Build()`
  - `Build()` method kept for backward compatibility

## P1 — Important for public NuGet

- [ ] **Split NuGet packages to reduce dependency footprint** *(deferred — split when real users ask for it)*
  - `NBatch` — Core interfaces, in-memory repo, file reader/writer (zero heavy dependencies)
  - `NBatch.SqlServer` — EF repo + SQL Server provider
  - `NBatch.PostgreSql` — EF repo + PostgreSQL provider
  - Currently every consumer pulls Dapper, SqlClient, EF Core SqlServer, EF Core PostgreSQL, NamingConventions

- [x] **Make `StepContext` internal, create a slim public type**
  - `StepContext` and `SkipContext` are now `internal`
  - `IStep.ProcessAsync` no longer takes `StepContext` — steps load their own context from the repository
  - `GetStartIndexAsync` moved from `IJobRepository` to `IStepRepository`

- [x] **Hide `IStepRepository` from `IStep.ProcessAsync`**
  - `IStepRepository` is now `internal`
  - Repository injected via constructor into `Step` and `TaskletStep`
  - `SkipPolicy.IsSatisfiedByAsync` changed to `internal`
  - Added `InternalsVisibleTo("DynamicProxyGenAssembly2")` for Moq

- [x] **Add `SkipPolicy.For<T>()` fluent factory**
- Compile-time safety via generic constraints (no `typeof(string)` accidents)
- Overloads for 1–3 exception types
- Original constructors kept for advanced use cases

- [x] **Add `WriteTo(Func<IEnumerable<T>, Task>)` lambda overload**
  - Inline writers for quick debug/demo scenarios without a dedicated class
  - Internal `DelegateWriter<T>` mirrors existing `DelegateProcessor<T>` pattern

- [x] **Throw on duplicate `ProcessWith` calls**
  - `InvalidOperationException` if `ProcessWith` is called more than once per step
  - Prevents silent overwrite of the processor

## P2 — Nice to have

- [ ] **Add `ILogger` support**
  - Add `Microsoft.Extensions.Logging.Abstractions` (lightweight, no runtime dependency)
  - Log chunk progress, retry attempts, skip decisions, job/step start/complete

- [x] **Change `IWriter.WriteAsync` return type from `bool` to `Task`**
  - All implementations now return `Task` instead of `Task<bool>`
  - `MsSqlWriter` throws `InvalidOperationException` on partial failure instead of returning `false`

- [x] **Fix `SkipContext.ExceptionDetail` null safety**
  - Changed `Exception.StackTrace!` to `Exception.StackTrace ?? string.Empty`
