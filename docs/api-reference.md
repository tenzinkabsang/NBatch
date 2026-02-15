---
layout: default
title: API Reference
nav_order: 7
---

# API Reference

A concise reference for NBatch's public types and interfaces.

---

## `Job`

A configured batch job containing one or more steps executed in sequence.

| Method | Returns | Description |
|--------|---------|-------------|
| `CreateBuilder(string jobName)` | `JobBuilder` | Creates a new job builder (static) |
| `RunAsync(CancellationToken)` | `Task<JobResult>` | Executes all steps in order |

---

## `JobBuilder`

Fluent builder for configuring and creating a `Job`.

| Method | Returns | Description |
|--------|---------|-------------|
| `UseJobStore(string connStr, DatabaseProvider provider = SqlServer)` | `JobBuilder` | Enables SQL-backed progress tracking |
| `WithLogger(ILogger logger)` | `JobBuilder` | Sets the logger for diagnostics |
| `WithListener(IJobListener listener)` | `JobBuilder` | Registers a job-level listener |
| `AddStep(string name, Func<..., IStepBuilderFinal> configure)` | `JobBuilder` | Adds a named step |
| `Build()` | `Job` | Creates the configured job |

---

## Step Builder (Fluent Chain)

The `AddStep` delegate receives a builder that flows through these stages:

### Stage 1: `IStepBuilderReadFrom`

| Method | Returns | Description |
|--------|---------|-------------|
| `ReadFrom<T>(IReader<T>)` | `IStepBuilderProcess<T>` | Sets the reader |
| `Execute(ITasklet)` | `ITaskletStepBuilder` | Creates a tasklet step |
| `Execute(Func<Task>)` | `ITaskletStepBuilder` | Creates a tasklet from a lambda |
| `Execute(Func<CancellationToken, Task>)` | `ITaskletStepBuilder` | Creates a tasklet with cancellation |

### Stage 2: `IStepBuilderProcess<TInput>`

| Method | Returns | Description |
|--------|---------|-------------|
| `ProcessWith<TOutput>(IProcessor<TIn, TOut>)` | `IStepBuilderWriteTo<TOutput>` | Sets the processor |
| `ProcessWith<TOutput>(Func<TIn, TOut>)` | `IStepBuilderWriteTo<TOutput>` | Sets a lambda processor |
| `WriteTo(IWriter<TInput>)` | `IStepBuilderOptions` | Skips processing, goes to writer |
| `WriteTo(Func<IEnumerable<TInput>, Task>)` | `IStepBuilderOptions` | Lambda writer, no processor |

### Stage 3: `IStepBuilderWriteTo<TOutput>`

| Method | Returns | Description |
|--------|---------|-------------|
| `WriteTo(IWriter<TOutput>)` | `IStepBuilderOptions` | Sets the writer |
| `WriteTo(Func<IEnumerable<TOutput>, Task>)` | `IStepBuilderOptions` | Sets a lambda writer |

### Stage 4: `IStepBuilderOptions`

| Method | Returns | Description |
|--------|---------|-------------|
| `WithSkipPolicy(SkipPolicy)` | `IStepBuilderOptions` | Sets the error skip policy |
| `WithChunkSize(int)` | `IStepBuilderOptions` | Sets the chunk size (default: 10) |
| `WithListener(IStepListener)` | `IStepBuilderOptions` | Registers a step-level listener |

---

## Core Interfaces

### `IReader<TItem>`

```csharp
public interface IReader<TItem>
{
    Task<IEnumerable<TItem>> ReadAsync(
        long startIndex, int chunkSize,
        CancellationToken cancellationToken = default);
}
```

### `IProcessor<TInput, TOutput>`

```csharp
public interface IProcessor<TInput, TOutput>
{
    Task<TOutput> ProcessAsync(
        TInput input,
        CancellationToken cancellationToken = default);
}
```

### `IWriter<TItem>`

```csharp
public interface IWriter<TItem>
{
    Task WriteAsync(
        IEnumerable<TItem> items,
        CancellationToken cancellationToken = default);
}
```

### `ITasklet`

```csharp
public interface ITasklet
{
    Task ExecuteAsync(CancellationToken cancellationToken = default);
}
```

### `IJobListener`

```csharp
public interface IJobListener
{
    Task BeforeJobAsync(string jobName, CancellationToken cancellationToken);
    Task AfterJobAsync(JobResult result, CancellationToken cancellationToken);
}
```

### `IStepListener`

```csharp
public interface IStepListener
{
    Task BeforeStepAsync(string stepName, CancellationToken cancellationToken);
    Task AfterStepAsync(StepResult result, CancellationToken cancellationToken);
}
```

---

## Result Types

### `JobResult`

```csharp
public record JobResult(
    string Name,
    bool Success,
    IReadOnlyList<StepResult> Steps);
```

### `StepResult`

```csharp
public record StepResult(
    string Name,
    bool Success,
    int ItemsRead = 0,
    int ItemsProcessed = 0,
    int ErrorsSkipped = 0);
```

---

## `SkipPolicy`

| Method | Description |
|--------|-------------|
| `SkipPolicy.None` | Never skip (default) |
| `SkipPolicy.For<TEx>(int maxSkips)` | Skip up to N items for one exception type |
| `SkipPolicy.For<T1, T2>(int maxSkips)` | Skip for two exception types |
| `SkipPolicy.For<T1, T2, T3>(int maxSkips)` | Skip for three exception types |

---

## `DatabaseProvider`

```csharp
public enum DatabaseProvider
{
    SqlServer,    // Microsoft SQL Server
    PostgreSql,   // PostgreSQL via Npgsql
    Sqlite        // SQLite
}
```

---

## Built-in Components

| Class | Type | Description |
|-------|------|-------------|
| `CsvReader<T>` | Reader | Delimited file reader with header parsing |
| `DbReader<T>` | Reader | EF Core paginated reader |
| `DbWriter<T>` | Writer | EF Core bulk writer |
| `FlatFileItemWriter<T>` | Writer | Delimited file writer |

---

**Next:** [Examples &rarr;](examples)
