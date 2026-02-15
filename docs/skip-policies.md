---
layout: default
title: Skip Policies
nav_order: 4
---

# Skip Policies

Skip policies let your job **tolerate errors** in individual records without aborting the entire step. When a skippable exception occurs, NBatch logs it, increments the skip counter, and moves on to the next item.

---

## Basic Usage

```csharp
.AddStep("import", step => step
    .ReadFrom(reader)
    .WriteTo(writer)
    .WithSkipPolicy(SkipPolicy.For<FlatFileParseException>(maxSkips: 5)))
```

This tells the step: "If a `FlatFileParseException` is thrown, skip that record. If more than 5 records fail, abort the step."

---

## Creating Skip Policies

### Single Exception Type

```csharp
SkipPolicy.For<FlatFileParseException>(maxSkips: 10)
```

### Multiple Exception Types

```csharp
SkipPolicy.For<FlatFileParseException, FormatException>(maxSkips: 5)

// Up to three types
SkipPolicy.For<FlatFileParseException, FormatException, InvalidOperationException>(maxSkips: 5)
```

### No Skipping (Default)

If you don't specify a skip policy, the step uses `SkipPolicy.None` � any exception aborts the step immediately.

```csharp
SkipPolicy.None  // default � no tolerance for errors
```

---

## How It Works

1. During chunk processing, if an exception is thrown during read or process�
2. NBatch checks if the exception type matches the skip policy.
3. If it matches **and** the skip count is below the limit ? the item is skipped.
4. If it doesn't match, or the limit is exceeded ? the exception propagates and the step fails.
5. Skipped errors are persisted in the job store (when enabled) for auditing.

---

## Monitoring Skips

The `StepResult` reports how many items were skipped:

```csharp
var result = await job.RunAsync();

foreach (var step in result.Steps)
{
    Console.WriteLine($"Step: {step.Name}");
    Console.WriteLine($"  Read:    {step.ItemsRead}");
    Console.WriteLine($"  Written: {step.ItemsProcessed}");
    Console.WriteLine($"  Skipped: {step.ErrorsSkipped}");
}
```

---

## Best Practices

- **Be specific** about exception types � avoid `SkipPolicy.For<Exception>(...)` which swallows everything.
- **Set reasonable limits** � a high skip count may mask a systemic problem in your data.
- **Use listeners** to alert when skips occur � combine with [`IStepListener`](listeners) for monitoring.
- **Enable the job store** to persist skip details for post-mortem analysis.

---

**Next:** [Job Store ?](job-store)
