---
layout: default
title: Listeners
nav_order: 6
---

# Listeners

Listeners let you hook into the **job and step lifecycle** for cross-cutting concerns like logging, metrics, notifications, or auditing.

---

## Job Listeners

Implement `IJobListener` to receive callbacks before and after a job executes:

```csharp
public interface IJobListener
{
    Task BeforeJobAsync(string jobName, CancellationToken cancellationToken);
    Task AfterJobAsync(JobResult result, CancellationToken cancellationToken);
}
```

Both methods have **no-op defaults** � implement only what you need.

### Example

```csharp
public class TimingListener : IJobListener
{
    private Stopwatch _sw = default!;

    public Task BeforeJobAsync(string jobName, CancellationToken ct)
    {
        _sw = Stopwatch.StartNew();
        Console.WriteLine($"Job '{jobName}' starting...");
        return Task.CompletedTask;
    }

    public Task AfterJobAsync(JobResult result, CancellationToken ct)
    {
        _sw.Stop();
        Console.WriteLine($"Job '{result.Name}' finished in {_sw.Elapsed} � Success: {result.Success}");
        return Task.CompletedTask;
    }
}
```

### Registration

```csharp
var job = Job.CreateBuilder("my-job")
    .WithListener(new TimingListener())
    .AddStep("work", step => step
        .ReadFrom(reader)
        .WriteTo(writer))
    .Build();
```

---

## Step Listeners

Implement `IStepListener` to receive callbacks before and after each step:

```csharp
public interface IStepListener
{
    Task BeforeStepAsync(string stepName, CancellationToken cancellationToken);
    Task AfterStepAsync(StepResult result, CancellationToken cancellationToken);
}
```

### Example

```csharp
public class StepMetricsListener : IStepListener
{
    public Task AfterStepAsync(StepResult result, CancellationToken ct)
    {
        Console.WriteLine(
            $"Step '{result.Name}': Read={result.ItemsRead}, " +
            $"Processed={result.ItemsProcessed}, Skipped={result.ErrorsSkipped}");
        return Task.CompletedTask;
    }
}
```

### Registration

Step listeners are registered **per step**:

```csharp
var job = Job.CreateBuilder("my-job")
    .AddStep("import", step => step
        .ReadFrom(reader)
        .WriteTo(writer)
        .WithListener(new StepMetricsListener()))
    .Build();
```

---

## Combining Listeners

You can register multiple listeners at both levels:

```csharp
var job = Job.CreateBuilder("monitored-job")
    .WithListener(new TimingListener())          // job-level
    .WithListener(new SlackNotifyListener())     // job-level
    .AddStep("extract", step => step
        .ReadFrom(reader)
        .WriteTo(writer)
        .WithListener(new StepMetricsListener())   // step-level
        .WithListener(new StepLoggingListener()))   // step-level
    .AddStep("notify", step => step
        .Execute(() => SendEmailAsync()))
    .Build();
```

---

## Common Use Cases

| Use Case | Listener Type | Implementation |
|----------|---------------|----------------|
| Job timing | `IJobListener` | Start/stop a `Stopwatch` |
| Slack/email alerts | `IJobListener` | Send notification in `AfterJobAsync` on failure |
| Per-step metrics | `IStepListener` | Log `ItemsRead`, `ItemsProcessed`, `ErrorsSkipped` |
| Audit logging | Both | Write entries to an audit trail |
| Health checks | `IJobListener` | Update a health-check endpoint |

---

**Next:** [API Reference ?](api-reference)
