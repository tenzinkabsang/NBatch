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

If you don't specify a skip policy, the step uses `SkipPolicy.None` -- any exception aborts the step immediately.

```csharp
SkipPolicy.None  // default -- no tolerance for errors
```

---

## How It Works

Skipping is **item-granular**. NBatch always tries the fast path first — read a whole
chunk, process every item, write the batch. Only when something in the chunk fails does
it fall back to an item-at-a-time scan:

1. A chunk fails during read, process, or write.
2. NBatch checks whether the exception matches the skip policy (see [Matching](#matching-rules) below). If it doesn't match, the exception propagates and the step fails immediately.
3. If it matches, the chunk is re-handled **one item at a time**: each item is processed and written individually. Read failures re-read the chunk range one position at a time.
4. Items that fail individually consult the skip policy: below the limit &mdash; that single item is skipped (good items in the same chunk are still written); limit exhausted &mdash; the exception propagates and the step fails.
5. Skipped errors are persisted in the job store (when enabled) for auditing, with the failing item's index.

### Matching rules

A thrown exception matches the policy when **any** of the following is a skippable type:

- the exception itself,
- a **base class** match — `SkipPolicy.For<IOException>` matches `FileNotFoundException`,
- anything in its **inner-exception chain** (walked up to 10 levels) — a policy for `FormatException` matches a `FlatFileParseException` that wraps one.

### Two things to know

- **Processors are re-invoked.** The items of a failed chunk are processed again during
  the scan, so processors should be idempotent. Key behavior off the item's data, not
  off call counts or external state.
- **At-least-once on restart.** If the skip limit is exhausted mid-chunk, the items
  already written in that chunk stay written; the step fails and the next run re-processes
  the chunk from its start.

---

## Retry Policies

For **transient** errors (timeouts, connection blips), skipping is the wrong tool — the
record is fine, the infrastructure hiccuped. Retry policies handle these: matching
failures are retried **before** the skip policy is consulted, so a transient error that
succeeds on retry consumes no skip budget.

```csharp
.AddStep("import", step => step
    .ReadFrom(reader)
    .WriteTo(writer)
    .WithRetryPolicy(RetryPolicy.For<TimeoutException>(maxAttempts: 3, TimeSpan.FromSeconds(2)))
    .WithSkipPolicy(SkipPolicy.For<FormatException>(maxSkips: 5)))
```

- `maxAttempts` is the total including the first try — `3` means "try, then retry twice".
- The optional delay waits between attempts; omit it to retry immediately.
- Exponential backoff: `RetryPolicy.For<TimeoutException>(4, TimeSpan.FromSeconds(1)).WithBackoffMultiplier(2)`
  waits 1s, 2s, then 4s.
- Matching follows the same rules as skip policies (subclasses + inner-exception chain).
- Retries apply to the chunk attempt and to each item during the scan; delays are
  interrupted by cancellation.

The full error-handling order for a failure is therefore: **retry → item-level scan → skip → fail**.
A persistent-but-skippable error is retried to exhaustion, then skipped once; a
persistent non-skippable error is retried to exhaustion, then fails the step. Note that
retrying a chunk re-invokes the processor for the whole chunk — one more reason
processors should be idempotent.

A job-wide default can be set with `JobBuilder.WithDefaultRetryPolicy(...)`, overridable per step.

---

## Monitoring Skips

The `StepResult` reports how many individual items were skipped:

```csharp
var result = await job.RunAsync();

foreach (var step in result.Steps)
{
    Console.WriteLine($"Step: {step.Name}");
    Console.WriteLine($"  Read:    {step.ItemsRead}");
    Console.WriteLine($"  Written: {step.ItemsWritten}");
    Console.WriteLine($"  Skipped: {step.ItemsSkipped}");
}
```

---

## Best Practices

- **Be specific** about exception types -- avoid `SkipPolicy.For<Exception>(...)` which swallows everything. Matching includes subclasses, so a base type covers its whole hierarchy.
- **Don't skip I/O exceptions broadly** -- a policy for `IOException` would also match `FileNotFoundException`, turning a missing file into a slow, budget-burning failure instead of an immediate one.
- **Set reasonable limits** -- a high skip count may mask a systemic problem in your data.
- **Keep processors idempotent** -- items of a failed chunk are re-processed during the item-level scan.
- **Use listeners** to alert when skips occur -- combine with [`IStepListener`](listeners) for monitoring.
- **Enable the [job store](job-store)** to persist skip details for post-mortem analysis.

---

**Next:** [Job Store &rarr;](job-store)
