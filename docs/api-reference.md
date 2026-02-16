---
layout: default
title: API Reference
nav_order: 8
---

# API Reference

A concise reference for NBatch's public types and interfaces.

---

## Package: `NBatch`

### `Job`

A configured batch job containing one or more steps executed in sequence.

| Method | Returns | Description |
|--------|---------|-------------|
| `CreateBuilder(string jobName)` | `JobBuilder` | Creates a new job builder (static) |
| `RunAsync(CancellationToken)` | `Task<JobResult>` | Executes all steps in order |

---

### `JobBuilder`

Fluent builder for configuring and creating a `Job`.

| Method | Returns | Description |
|--------|---------|-------------|
| `WithLogger(ILogger logger)` | `JobBuilder` | Sets the logger for diagnostics |
| `WithListener(IJobListener listener)` | `JobBuilder` | Registers a job-level listener |
| `AddStep(string name, Func<..., IStepBuilderFinal> configure)` | `JobBuilder` | Adds a named step |
| `Build()` | `Job` | Creates the configured job |

> **Note:** `UseJobStore()` is an extension method provided by the `NBatch.EntityFrameworkCore` package. See [below](#package-nbatchentityframeworkcore).

---

### Step Builder (Fluent Chain)

The `AddStep` delegate receives a builder that flows through these stages:

#### Stage 1: `IStepBuilderReadFrom`

| Method | Returns | Description |
|--------|---------|-------------|
| `ReadFrom<T>(IReader<T>)` | `IStepBuilderProcess<T>` | Sets the reader |
| `Execute(ITasklet)` | `ITaskletStepBuilder` | Creates a tasklet step |
| `Execute(Func<Task>)` | `ITaskletStepBuilder` | Creates a tasklet from an async lambda |
| `Execute(Func<CancellationToken, Task>)` | `ITaskletStepBuilder` | Creates a tasklet with cancellation |
| `Execute(Action)` | `ITaskletStepBuilder` | Creates a tasklet from a synchronous action |

#### Stage 2: `IStepBuilderProcess<TInput>`

| Method | Returns | Description |
|--------|---------|-------------|
| `ProcessWith<TOutput>(IProcessor<TIn, TOut>)` | `IStepBuilderWriteTo<TOutput>` | Sets the processor (interface) |
| `ProcessWith<TOutput>(Func<TIn, TOut>)` | `IStepBuilderWriteTo<TOutput>` | Synchronous lambda processor |
| `ProcessWith<TOutput>(Func<TIn, CancellationToken, Task<TOut>>)` | `IStepBuilderWriteTo<TOutput>` | Async lambda processor |
| `WriteTo(IWriter<TInput>)` | `IStepBuilderOptions` | Skips processing, goes to writer |
| `WriteTo(Func<IEnumerable<TInput>, Task>)` | `IStepBuilderOptions` | Lambda writer, no processor |
| `WriteTo(Func<IEnumerable<TInput>, CancellationToken, Task>)` | `IStepBuilderOptions` | Lambda writer with cancellation |

#### Stage 3: `IStepBuilderWriteTo<TOutput>`

| Method | Returns | Description |
|--------|---------|-------------|
| `WriteTo(IWriter<TOutput>)` | `IStepBuilderOptions` | Sets the writer (interface) |
| `WriteTo(Func<IEnumerable<TOutput>, Task>)` | `IStepBuilderOptions` | Lambda writer |
| `WriteTo(Func<IEnumerable<TOutput>, CancellationToken, Task>)` | `IStepBuilderOptions` | Lambda writer with cancellation |

#### Stage 4: `IStepBuilderOptions`

| Method | Returns | Description |
|--------|---------|-------------|
| `WithSkipPolicy(SkipPolicy)` | `IStepBuilderOptions` | Sets the error skip policy |
| `WithChunkSize(int)` | `IStepBuilderOptions` | Sets the chunk size (default: 10) |
| `WithListener(IStepListener)` | `IStepBuilderOptions` | Registers a step-level listener |

#### `ITaskletStepBuilder`

| Method | Returns | Description |
|--------|---------|-------------|
| `WithListener(IStepListener)` | `ITaskletStepBuilder` | Registers a listener for the tasklet step |

---

### Core Interfaces

#### `IReader<TItem>`

```csharp
public interface IReader<TItem>
{
    Task<IEnumerable<TItem>> ReadAsync(
        long startIndex, int chunkSize,
        CancellationToken cancellationToken = default);
}
```

#### `IProcessor<TInput, TOutput>`

```csharp
public interface IProcessor<TInput, TOutput>
{
    Task<TOutput> ProcessAsync(
        TInput input,
        CancellationToken cancellationToken = default);
}
```

#### `IWriter<TItem>`

```csharp
public interface IWriter<TItem>
{
    Task WriteAsync(
        IEnumerable<TItem> items,
        CancellationToken cancellationToken = default);
}
```

#### `ITasklet`

```csharp
public interface ITasklet
{
    Task ExecuteAsync(CancellationToken cancellationToken = default);
}
```

#### `IJobListener`

```csharp
public interface IJobListener
{
    Task BeforeJobAsync(string jobName, CancellationToken cancellationToken);   // default: no-op
    Task AfterJobAsync(JobResult result, CancellationToken cancellationToken);  // default: no-op
}
```

#### `IStepListener`

```csharp
public interface IStepListener
{
    Task BeforeStepAsync(string stepName, CancellationToken cancellationToken);   // default: no-op
    Task AfterStepAsync(StepResult result, CancellationToken cancellationToken);  // default: no-op
}
```

---

### Dependency Injection

#### `ServiceCollectionExtensions`

| Method | Description |
|--------|-------------|
| `AddNBatch(this IServiceCollection, Action<NBatchBuilder>)` | Registers NBatch services, job factories, and background workers |

#### `NBatchBuilder`

| Method | Returns | Description |
|--------|---------|-------------|
| `AddJob(string name, Action<JobBuilder>)` | `JobRegistration` | Registers a named job |
| `AddJob(string name, Action<IServiceProvider, JobBuilder>)` | `JobRegistration` | Registers a named job with access to DI services |

#### `JobRegistration`

| Method | Returns | Description |
|--------|---------|-------------|
| `RunOnce()` | `JobRegistration` | Runs the job once at startup, then the worker exits |
| `RunEvery(TimeSpan interval)` | `JobRegistration` | Runs immediately, then repeats after each interval |

> Jobs without a schedule are on-demand only &mdash; trigger them by injecting `IJobRunner` and calling `RunAsync("job-name")`.

#### `IJobRunner`

```csharp
public interface IJobRunner
{
    Task<JobResult> RunAsync(string jobName, CancellationToken cancellationToken = default);
}
```

Inject `IJobRunner` into controllers, endpoints, or services to trigger jobs on demand.

---

### Result Types

#### `JobResult`

```csharp
public record JobResult(
    string Name,
    bool Success,
    IReadOnlyList<StepResult> Steps);
```

#### `StepResult`

```csharp
public record StepResult(
    string Name,
    bool Success,
    int ItemsRead = 0,
    int ItemsProcessed = 0,
    int ErrorsSkipped = 0);
```

---

### `SkipPolicy`

| Method | Description |
|--------|-------------|
| `SkipPolicy.None` | Never skip (default) |
| `SkipPolicy.For<TEx>(int maxSkips)` | Skip up to N items for one exception type |
| `SkipPolicy.For<T1, T2>(int maxSkips)` | Skip for two exception types |
| `SkipPolicy.For<T1, T2, T3>(int maxSkips)` | Skip for three exception types |

---

### Built-in Components

| Class | Type | Package | Description |
|-------|------|---------|-------------|
| `CsvReader<T>` | Reader | `NBatch` | Delimited file reader with header parsing |
| `DbReader<T>` | Reader | `NBatch` | EF Core paginated reader |
| `DbWriter<T>` | Writer | `NBatch` | EF Core bulk writer |
| `FlatFileItemWriter<T>` | Writer | `NBatch` | Delimited file writer |

---

## Package: `NBatch.EntityFrameworkCore`

The EF Core job store &mdash; install separately:

```bash
dotnet add package NBatch.EntityFrameworkCore
```

### `JobBuilderExtensions`

| Method | Returns | Description |
|--------|---------|-------------|
| `UseJobStore(this JobBuilder, string connStr, DatabaseProvider provider = SqlServer)` | `JobBuilder` | Enables SQL-backed progress tracking for restart-from-failure |

### `DatabaseProvider`

```csharp
public enum DatabaseProvider
{
    SqlServer,    // Microsoft SQL Server
    PostgreSql,   // PostgreSQL via Npgsql
    Sqlite,       // SQLite
    MySql         // MySQL / MariaDB via Pomelo (.NET 8 & 9 only)
}
```

---

**Next:** [Examples &rarr;](examples)
