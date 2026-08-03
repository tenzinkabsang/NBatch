using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace NBatch.Core;

/// <summary>
/// OpenTelemetry-ready instrumentation. NBatch emits an activity per job run
/// (<c>nbatch.job</c>) and per step (<c>nbatch.step</c>), plus item counters and
/// a step-duration histogram — all under the source/meter name
/// <see cref="SourceName"/>:
/// <code>
/// tracing.AddSource(NBatchDiagnostics.SourceName);
/// metrics.AddMeter(NBatchDiagnostics.SourceName);
/// </code>
/// </summary>
public static class NBatchDiagnostics
{
    /// <summary>Name of both the <see cref="System.Diagnostics.ActivitySource"/> and the meter.</summary>
    public const string SourceName = "NBatch";

    private static readonly string? Version = typeof(NBatchDiagnostics).Assembly.GetName().Version?.ToString();

    internal static readonly ActivitySource ActivitySource = new(SourceName, Version);
    internal static readonly Meter Meter = new(SourceName, Version);

    internal static readonly Counter<long> ItemsRead =
        Meter.CreateCounter<long>("nbatch.items.read", "{item}", "Items read by steps");
    internal static readonly Counter<long> ItemsWritten =
        Meter.CreateCounter<long>("nbatch.items.written", "{item}", "Items written by steps");
    internal static readonly Counter<long> ItemsSkipped =
        Meter.CreateCounter<long>("nbatch.items.skipped", "{item}", "Items skipped by skip policies");
    internal static readonly Histogram<double> StepDuration =
        Meter.CreateHistogram<double>("nbatch.step.duration", "s", "Step execution duration");
}
