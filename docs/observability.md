---
layout: default
title: Observability
nav_order: 8
---

# Observability

NBatch emits OpenTelemetry-ready **traces** and **metrics** out of the box, with zero
extra dependencies — everything is built on in-box `System.Diagnostics`. Opt in by
subscribing to the source/meter name exposed as `NBatchDiagnostics.SourceName`
(`"NBatch"`):

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddSource(NBatchDiagnostics.SourceName)
        .AddOtlpExporter())
    .WithMetrics(metrics => metrics
        .AddMeter(NBatchDiagnostics.SourceName)
        .AddOtlpExporter());
```

---

## Traces

| Activity | Emitted | Tags |
|----------|---------|------|
| `nbatch.job` | Once per job run | `nbatch.job.name`, `nbatch.job.success`, `nbatch.job.cancelled` |
| `nbatch.step` | Once per step, nested under the job activity | `nbatch.job.name`, `nbatch.step.name`, `nbatch.step.success`, `nbatch.step.items_read`, `nbatch.step.items_written`, `nbatch.step.items_skipped` |

Failed or cancelled runs set the activity status to `Error`, so they surface directly
in trace-based alerting.

---

## Metrics

| Instrument | Type | Unit | Description |
|------------|------|------|-------------|
| `nbatch.items.read` | Counter | `{item}` | Items read by steps |
| `nbatch.items.written` | Counter | `{item}` | Items written by steps |
| `nbatch.items.skipped` | Counter | `{item}` | Items skipped by skip policies |
| `nbatch.step.duration` | Histogram | `s` | Step execution duration |

All measurements are tagged with `nbatch.job.name` and `nbatch.step.name`, so you can
chart throughput and skip rates per job or per step.

---

## Durations on Results

Both result records carry wall-clock timings, useful for logs and listeners without
any telemetry pipeline:

```csharp
var result = await job.RunAsync();
Console.WriteLine($"{result.Name} took {result.Duration}");
foreach (var step in result.Steps)
    Console.WriteLine($"  {step.Name}: {step.Duration}");
```

For lifecycle hooks (alerts, audit trails), see [Listeners](listeners).

---

**Next:** [API Reference &rarr;](api-reference)
