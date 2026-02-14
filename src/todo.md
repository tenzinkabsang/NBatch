# NBatch v2.0.0 — TODO

## P0 — Must fix before release

- [x] **Add `CancellationToken` to all interfaces**
  - `IReader.ReadAsync`, `IProcessor.ProcessAsync`, `IWriter.WriteAsync`, `ITasklet.ExecuteAsync`, `Job.RunAsync`
  - Adding later is a breaking change to every interface
  - Thread through `Step.ProcessChunkAsync`, `EfJobRepository`, listeners

- [x] **Integration tests for restart-from-failure**
  - Job restart from a failed chunk (the headline feature)
  - `StepContext.RetryPreviousIfFailed` with various offsets
  - `RetryPolicy` integration with `ProcessChunkAsync`
  - `TaskletStep` error handling
  - `EfJobRepository` with SQLite provider (`DatabaseProvider.Sqlite` added)

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

## P2 — Nice to have

- [ ] **Add `ILogger` support**
  - Add `Microsoft.Extensions.Logging.Abstractions` (lightweight, no runtime dependency)
  - Log chunk progress, retry attempts, skip decisions, job/step start/complete

- [x] **Change `IWriter.WriteAsync` return type from `bool` to `Task`**
  - All implementations now return `Task` instead of `Task<bool>`
  - `MsSqlWriter` throws `InvalidOperationException` on partial failure instead of returning `false`

- [x] **Fix `SkipContext.ExceptionDetail` null safety**
  - Changed `Exception.StackTrace!` to `Exception.StackTrace ?? string.Empty`
