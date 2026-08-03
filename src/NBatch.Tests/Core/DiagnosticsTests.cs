using System.Diagnostics;
using System.Diagnostics.Metrics;
using NBatch.Core;
using NBatch.Core.Interfaces;
using NUnit.Framework;

namespace NBatch.Tests.Core;

/// <summary>
/// In-process verification of the OpenTelemetry-ready instrumentation:
/// activities per job/step and item counters + duration histogram.
/// </summary>
[TestFixture]
[NonParallelizable] // listeners observe process-global sources
internal sealed class DiagnosticsTests
{
    private sealed class ListReader<T>(IReadOnlyList<T> items) : IReader<T>
    {
        public Task<IEnumerable<T>> ReadAsync(long startIndex, int chunkSize, CancellationToken cancellationToken = default)
            => Task.FromResult(items.Skip((int)startIndex).Take(chunkSize));
    }

    private sealed class NullWriter<T> : IWriter<T>
    {
        public Task WriteAsync(IEnumerable<T> items, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private static ActivityListener CreateListener(List<Activity> completed)
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == NBatchDiagnostics.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activity => { lock (completed) completed.Add(activity); }
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }

    [Test]
    public async Task Job_run_emits_job_and_step_activities_with_tags()
    {
        var completed = new List<Activity>();
        using var listener = CreateListener(completed);

        var job = Job.CreateBuilder("traced-job")
            .AddStep("step-a", step => step
                .ReadFrom(new ListReader<string>(["a", "b", "c"]))
                .WriteTo(new NullWriter<string>())
                .WithChunkSize(2))
            .Build();

        await job.RunAsync();

        var jobActivity = completed.Single(a => a.OperationName == "nbatch.job");
        Assert.That(jobActivity.GetTagItem("nbatch.job.name"), Is.EqualTo("traced-job"));
        Assert.That(jobActivity.GetTagItem("nbatch.job.success"), Is.EqualTo(true));
        Assert.That(jobActivity.GetTagItem("nbatch.job.cancelled"), Is.EqualTo(false));

        var stepActivity = completed.Single(a => a.OperationName == "nbatch.step");
        Assert.That(stepActivity.GetTagItem("nbatch.step.name"), Is.EqualTo("step-a"));
        Assert.That(stepActivity.GetTagItem("nbatch.step.items_read"), Is.EqualTo(3));
        Assert.That(stepActivity.GetTagItem("nbatch.step.items_written"), Is.EqualTo(3));
        Assert.That(stepActivity.GetTagItem("nbatch.step.items_skipped"), Is.EqualTo(0));
    }

    [Test]
    public async Task Failed_step_sets_error_status()
    {
        var completed = new List<Activity>();
        using var listener = CreateListener(completed);

        var job = Job.CreateBuilder("failing-job")
            .AddStep("bad-step", step => step
                .Execute((Action)(() => throw new InvalidOperationException("boom"))))
            .Build();

        await job.RunAsync();

        var jobActivity = completed.Single(a => a.OperationName == "nbatch.job");
        Assert.That(jobActivity.Status, Is.EqualTo(ActivityStatusCode.Error));
        Assert.That(jobActivity.GetTagItem("nbatch.job.success"), Is.EqualTo(false));

        var stepActivity = completed.Single(a => a.OperationName == "nbatch.step");
        Assert.That(stepActivity.Status, Is.EqualTo(ActivityStatusCode.Error));
    }

    [Test]
    public async Task Counters_and_duration_histogram_are_recorded_per_step()
    {
        long itemsRead = 0, itemsWritten = 0, itemsSkipped = 0;
        var durations = new List<double>();

        using var meterListener = new MeterListener();
        meterListener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == NBatchDiagnostics.SourceName)
                l.EnableMeasurementEvents(instrument);
        };
        meterListener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
        {
            bool isThisJob = false;
            foreach (var tag in tags)
                if (tag.Key == "nbatch.job.name" && Equals(tag.Value, "metered-job"))
                    isThisJob = true;
            if (!isThisJob) return;

            switch (instrument.Name)
            {
                case "nbatch.items.read": Interlocked.Add(ref itemsRead, value); break;
                case "nbatch.items.written": Interlocked.Add(ref itemsWritten, value); break;
                case "nbatch.items.skipped": Interlocked.Add(ref itemsSkipped, value); break;
            }
        });
        meterListener.SetMeasurementEventCallback<double>((instrument, value, tags, _) =>
        {
            if (instrument.Name == "nbatch.step.duration")
                lock (durations) durations.Add(value);
        });
        meterListener.Start();

        var job = Job.CreateBuilder("metered-job")
            .AddStep("step-a", step => step
                .ReadFrom(new ListReader<string>(["a", "bad", "c"]))
                .ProcessWith<string>((string s) =>
                {
                    if (s == "bad") throw new FormatException("bad item");
                    return s;
                })
                .WriteTo(new NullWriter<string>())
                .WithSkipPolicy(SkipPolicy.For<FormatException>(maxSkips: 5))
                .WithChunkSize(3))
            .Build();

        await job.RunAsync();

        Assert.That(itemsRead, Is.EqualTo(3));
        Assert.That(itemsWritten, Is.EqualTo(2));
        Assert.That(itemsSkipped, Is.EqualTo(1));
        Assert.That(durations, Is.Not.Empty);
    }

    [Test]
    public async Task Result_durations_are_populated()
    {
        var job = Job.CreateBuilder("timed-job")
            .AddStep("wait", step => step
                .Execute(() => Task.Delay(30)))
            .Build();

        var result = await job.RunAsync();

        Assert.That(result.Duration, Is.GreaterThan(TimeSpan.Zero));
        Assert.That(result.Steps[0].Duration, Is.GreaterThan(TimeSpan.Zero));
        Assert.That(result.Duration, Is.GreaterThanOrEqualTo(result.Steps[0].Duration));
    }
}
