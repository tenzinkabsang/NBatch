using Cronos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NBatch.Core;
using NBatch.Core.Interfaces;
using NUnit.Framework;

namespace NBatch.Tests.Core;

[TestFixture]
internal sealed class HostedServiceTests
{
    #region Helpers

    private sealed class ListReader<T>(IReadOnlyList<T> items) : IReader<T>
    {
        public Task<IEnumerable<T>> ReadAsync(long startIndex, int chunkSize, CancellationToken cancellationToken = default)
        {
            var chunk = items.Skip((int)startIndex).Take(chunkSize);
            return Task.FromResult(chunk);
        }
    }

    private sealed class CountingWriter<T> : IWriter<T>
    {
        private int _writeCount;
        public int WriteCount => _writeCount;

        public Task WriteAsync(IEnumerable<T> items, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _writeCount);
            return Task.CompletedTask;
        }
    }

    #endregion

    #region JobRegistration

    [Test]
    public void RunOnce_sets_scheduled_flag()
    {
        var reg = new JobRegistration("test");
        Assert.That(reg.IsScheduled, Is.False);

        reg.RunOnce();

        Assert.That(reg.IsScheduled, Is.True);
        Assert.That(reg.IsRunOnce, Is.True);
        Assert.That(reg.Interval, Is.EqualTo(TimeSpan.Zero));
    }

    [Test]
    public void RunEvery_sets_scheduled_flag_and_interval()
    {
        var reg = new JobRegistration("test");
        reg.RunEvery(TimeSpan.FromMinutes(5));

        Assert.That(reg.IsScheduled, Is.True);
        Assert.That(reg.IsRunOnce, Is.False);
        Assert.That(reg.Interval, Is.EqualTo(TimeSpan.FromMinutes(5)));
    }

    [Test]
    public void RunEvery_rejects_zero_interval()
    {
        var reg = new JobRegistration("test");
        Assert.Throws<ArgumentOutOfRangeException>(() => reg.RunEvery(TimeSpan.Zero));
    }

    [Test]
    public void RunEvery_rejects_negative_interval()
    {
        var reg = new JobRegistration("test");
        Assert.Throws<ArgumentOutOfRangeException>(() => reg.RunEvery(TimeSpan.FromSeconds(-1)));
    }

    [Test]
    public void RunEvery_overrides_RunOnce()
    {
        var reg = new JobRegistration("test");
        reg.RunOnce();
        reg.RunEvery(TimeSpan.FromHours(1));

        Assert.That(reg.IsRunOnce, Is.False);
        Assert.That(reg.Interval, Is.EqualTo(TimeSpan.FromHours(1)));
    }

    [Test]
    public void RunOnce_overrides_RunEvery()
    {
        var reg = new JobRegistration("test");
        reg.RunEvery(TimeSpan.FromHours(1));
        reg.RunOnce();

        Assert.That(reg.IsRunOnce, Is.True);
        Assert.That(reg.Interval, Is.EqualTo(TimeSpan.Zero));
    }

    [Test]
    public void Job_without_schedule_is_not_scheduled()
    {
        var reg = new JobRegistration("test");
        Assert.That(reg.IsScheduled, Is.False);
    }

    #endregion

    #region Service registration

    [Test]
    public void AddNBatch_does_not_register_hosted_service_when_no_schedule()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddNBatch(nbatch =>
        {
            nbatch.AddJob("on-demand", job => job
                .AddStep("s1", step => step
                    .ReadFrom(new ListReader<string>(["a"]))
                    .WriteTo(new CountingWriter<string>())));
        });

        var sp = services.BuildServiceProvider();
        var hostedServices = sp.GetServices<IHostedService>();

        Assert.That(hostedServices.OfType<NBatchJobWorkerService>(), Is.Empty);
    }

    [Test]
    public void AddNBatch_registers_hosted_service_when_RunOnce_configured()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddNBatch(nbatch =>
        {
            nbatch.AddJob("startup-job", job => job
                .AddStep("s1", step => step
                    .ReadFrom(new ListReader<string>(["a"]))
                    .WriteTo(new CountingWriter<string>())))
                .RunOnce();
        });

        var sp = services.BuildServiceProvider();
        var hostedServices = sp.GetServices<IHostedService>();

        Assert.That(hostedServices.OfType<NBatchJobWorkerService>().Count(), Is.EqualTo(1));
    }

    [Test]
    public void AddNBatch_registers_hosted_service_when_RunEvery_configured()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddNBatch(nbatch =>
        {
            nbatch.AddJob("recurring-job", job => job
                .AddStep("s1", step => step
                    .ReadFrom(new ListReader<string>(["a"]))
                    .WriteTo(new CountingWriter<string>())))
                .RunEvery(TimeSpan.FromHours(1));
        });

        var sp = services.BuildServiceProvider();
        var hostedServices = sp.GetServices<IHostedService>();

        Assert.That(hostedServices.OfType<NBatchJobWorkerService>().Count(), Is.EqualTo(1));
    }

    [Test]
    public void AddNBatch_registers_multiple_hosted_services_for_multiple_scheduled_jobs()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddNBatch(nbatch =>
        {
            nbatch.AddJob("job-a", job => job
                .AddStep("s1", step => step
                    .ReadFrom(new ListReader<string>(["a"]))
                    .WriteTo(new CountingWriter<string>())))
                .RunOnce();

            nbatch.AddJob("job-b", job => job
                .AddStep("s1", step => step
                    .ReadFrom(new ListReader<string>(["b"]))
                    .WriteTo(new CountingWriter<string>())))
                .RunEvery(TimeSpan.FromMinutes(30));

            // No schedule — should not be registered as hosted service
            nbatch.AddJob("job-c", job => job
                .AddStep("s1", step => step
                    .ReadFrom(new ListReader<string>(["c"]))
                    .WriteTo(new CountingWriter<string>())));
        });

        var sp = services.BuildServiceProvider();
        var hostedServices = sp.GetServices<IHostedService>();

        Assert.That(hostedServices.OfType<NBatchJobWorkerService>().Count(), Is.EqualTo(2));
    }

    #endregion

    #region Worker execution

    [Test]
    public async Task RunOnce_worker_executes_job_then_completes()
    {
        var writer = new CountingWriter<string>();

        var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddNBatch(nbatch =>
                {
                    nbatch.AddJob("once-job", job => job
                        .AddStep("s1", step => step
                            .ReadFrom(new ListReader<string>(["a", "b"]))
                            .WriteTo(writer)))
                        .RunOnce();
                });
            })
            .Build();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await host.StartAsync(cts.Token);

        // Give the worker time to execute the run-once job
        await Task.Delay(500, cts.Token);
        await host.StopAsync(cts.Token);

        Assert.That(writer.WriteCount, Is.EqualTo(1));
    }

    [Test]
    public async Task RunEvery_worker_executes_job_multiple_times()
    {
        var writer = new CountingWriter<string>();

        var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddNBatch(nbatch =>
                {
                    nbatch.AddJob("recurring-job", job => job
                        .AddStep("s1", step => step
                            .ReadFrom(new ListReader<string>(["a"]))
                            .WriteTo(writer)))
                        .RunEvery(TimeSpan.FromMilliseconds(50));
                });
            })
            .Build();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await host.StartAsync(cts.Token);

        // Wait enough time for at least 2 runs (immediate + 1 interval)
        await Task.Delay(300, cts.Token);
        await host.StopAsync(cts.Token);

        Assert.That(writer.WriteCount, Is.GreaterThanOrEqualTo(2));
    }

    [Test]
    public async Task Worker_survives_job_failure_and_retries()
    {
        int callCount = 0;
        var writer = new CountingWriter<string>();

        var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddNBatch(nbatch =>
                {
                    nbatch.AddJob("flaky-job", job => job
                        .AddStep("s1", step => step
                            .ReadFrom(new ListReader<string>(["a"]))
                            .ProcessWith((string s) =>
                            {
                                if (Interlocked.Increment(ref callCount) == 1)
                                    throw new InvalidOperationException("Transient failure");
                                return s;
                            })
                            .WriteTo(writer)))
                        .RunEvery(TimeSpan.FromMilliseconds(50));
                });
            })
            .Build();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await host.StartAsync(cts.Token);

        // Wait for first run (fails) + interval + second run (succeeds)
        await Task.Delay(500, cts.Token);
        await host.StopAsync(cts.Token);

        // First call threw, second call should have written
        Assert.That(callCount, Is.GreaterThanOrEqualTo(2));
        Assert.That(writer.WriteCount, Is.GreaterThanOrEqualTo(1));
    }

    [Test]
    public async Task Worker_stops_cleanly_on_cancellation()
    {
        var writer = new CountingWriter<string>();

        var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddNBatch(nbatch =>
                {
                    nbatch.AddJob("cancel-job", job => job
                        .AddStep("s1", step => step
                            .ReadFrom(new ListReader<string>(["a"]))
                            .WriteTo(writer)))
                        .RunEvery(TimeSpan.FromHours(1)); // long interval
                });
            })
            .Build();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await host.StartAsync(cts.Token);

        // Let the first run complete, then stop immediately
        await Task.Delay(200, cts.Token);
        await host.StopAsync(cts.Token);

        // Should not throw — clean shutdown
        Assert.That(writer.WriteCount, Is.GreaterThanOrEqualTo(1));
    }

    [Test]
    public async Task Scheduled_jobs_are_still_available_via_IJobRunner()
    {
        var writer = new CountingWriter<string>();
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddNBatch(nbatch =>
        {
            nbatch.AddJob("dual-job", job => job
                .AddStep("s1", step => step
                    .ReadFrom(new ListReader<string>(["a"]))
                    .WriteTo(writer)))
                .RunOnce();
        });

        var sp = services.BuildServiceProvider();
        var runner = sp.GetRequiredService<IJobRunner>();

        // Should still be callable on-demand via IJobRunner
        var result = await runner.RunAsync("dual-job");

        Assert.That(result.Success, Is.True);
        Assert.That(writer.WriteCount, Is.EqualTo(1));
    }

    #endregion

    #region Cron & deferred-start registration

    [Test]
    public void RunOnCron_sets_scheduled_flag()
    {
        var reg = new JobRegistration("test").RunOnCron("0 2 * * *");

        Assert.That(reg.IsScheduled, Is.True);
        Assert.That(reg.IsRunOnce, Is.False);
        Assert.That(reg.CronSchedule, Is.Not.Null);
    }

    [TestCase("not a cron")]
    [TestCase("* * *")]
    [TestCase("99 * * * *")]
    public void RunOnCron_throws_ArgumentException_on_invalid_expression(string expression)
    {
        var reg = new JobRegistration("test");
        var ex = Assert.Throws<ArgumentException>(() => reg.RunOnCron(expression));
        Assert.That(ex!.Message, Does.Contain(expression));
    }

    [Test]
    public void RunOnCron_defaults_to_UTC_and_honors_time_zone()
    {
        var utcReg = new JobRegistration("test").RunOnCron("0 2 * * *");
        Assert.That(utcReg.TimeZone, Is.EqualTo(TimeZoneInfo.Utc));

        var local = TimeZoneInfo.Local;
        var zonedReg = new JobRegistration("test").RunOnCron("0 2 * * *", local);
        Assert.That(zonedReg.TimeZone, Is.EqualTo(local));
    }

    [Test]
    public void Schedule_calls_override_each_other_last_wins()
    {
        var reg = new JobRegistration("test");

        reg.RunEvery(TimeSpan.FromMinutes(5)).RunOnCron("0 2 * * *");
        Assert.That(reg.CronSchedule, Is.Not.Null);
        Assert.That(reg.Interval, Is.EqualTo(TimeSpan.Zero));

        reg.RunOnCron("0 2 * * *").RunEvery(TimeSpan.FromMinutes(5));
        Assert.That(reg.CronSchedule, Is.Null);
        Assert.That(reg.Interval, Is.EqualTo(TimeSpan.FromMinutes(5)));

        reg.RunOnCron("0 2 * * *").RunOnce();
        Assert.That(reg.CronSchedule, Is.Null);
        Assert.That(reg.IsRunOnce, Is.True);
    }

    [Test]
    public void RunEvery_records_deferred_start()
    {
        var reg = new JobRegistration("test").RunEvery(TimeSpan.FromMinutes(5), runImmediately: false);
        Assert.That(reg.RunImmediately, Is.False);

        reg.RunEvery(TimeSpan.FromMinutes(5));
        Assert.That(reg.RunImmediately, Is.True, "default is immediate");
    }

    [Test]
    public void AddNBatch_registers_hosted_service_when_RunOnCron_configured()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddNBatch(nbatch =>
        {
            nbatch.AddJob("cron-job", job => job
                .AddStep("s1", step => step.Execute(() => { })))
                .RunOnCron("0 2 * * *");
        });

        var sp = services.BuildServiceProvider();
        var workers = sp.GetServices<IHostedService>().OfType<NBatchJobWorkerService>();

        Assert.That(workers.Count(), Is.EqualTo(1));
    }

    [Test]
    public void Cronos_GetNextOccurrence_matches_expectation()
    {
        // Sanity-pins the occurrence semantics the worker relies on.
        var cron = CronExpression.Parse("0 2 * * *");
        var from = new DateTimeOffset(2030, 6, 15, 10, 0, 0, TimeSpan.Zero);

        var next = cron.GetNextOccurrence(from, TimeZoneInfo.Utc);

        Assert.That(next, Is.EqualTo(new DateTimeOffset(2030, 6, 16, 2, 0, 0, TimeSpan.Zero)));
    }

    #endregion

    #region Deterministic worker scheduling (FakeTimeProvider)

    private sealed class RecordingJobRunner : IJobRunner
    {
        private int _runCount;
        public int RunCount => _runCount;

        public Task<JobResult> RunAsync(string jobName, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _runCount);
            return Task.FromResult(new JobResult(jobName, true, []));
        }
    }

    private static async Task WaitUntilAsync(Func<bool> condition, int timeoutMs = 5000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (!condition() && DateTime.UtcNow < deadline)
            await Task.Delay(10);
    }

    [Test]
    public async Task Cron_worker_runs_at_next_occurrence()
    {
        var fakeTime = new FakeTimeProvider(new DateTimeOffset(2030, 1, 1, 0, 0, 30, TimeSpan.Zero));
        var runner = new RecordingJobRunner();
        var registration = new JobRegistration("cron-job").RunOnCron("*/5 * * * *");
        var worker = new NBatchJobWorkerService(runner, "cron-job", registration, NullLogger.Instance, fakeTime);

        await worker.StartAsync(CancellationToken.None);

        await Task.Delay(100);
        Assert.That(runner.RunCount, Is.EqualTo(0), "must not run before the cron occurrence");

        fakeTime.Advance(TimeSpan.FromMinutes(5)); // past 00:05:00
        await WaitUntilAsync(() => runner.RunCount >= 1);
        Assert.That(runner.RunCount, Is.EqualTo(1));

        await worker.StopAsync(CancellationToken.None);
    }

    private sealed class BlockingJobRunner : IJobRunner
    {
        private int _runCount;
        private readonly SemaphoreSlim _release = new(0);
        public int RunCount => _runCount;

        public void ReleaseRun() => _release.Release();

        public async Task<JobResult> RunAsync(string jobName, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _runCount);
            await _release.WaitAsync(cancellationToken);
            return new JobResult(jobName, true, []);
        }
    }

    [Test]
    public async Task Cron_worker_skips_occurrences_missed_while_a_run_is_in_progress()
    {
        // The job outlasts the cron cadence: occurrences that pass while a run is
        // in progress must be skipped, not executed back-to-back afterwards.
        var fakeTime = new FakeTimeProvider(new DateTimeOffset(2030, 1, 1, 0, 4, 0, TimeSpan.Zero));
        var runner = new BlockingJobRunner();
        var registration = new JobRegistration("cron-job").RunOnCron("*/5 * * * *");
        var worker = new NBatchJobWorkerService(runner, "cron-job", registration, NullLogger.Instance, fakeTime);

        await worker.StartAsync(CancellationToken.None);
        await Task.Delay(100); // let the worker arm its delay timer before advancing

        fakeTime.Advance(TimeSpan.FromSeconds(90)); // past 00:05 → run 1 starts and blocks
        await WaitUntilAsync(() => runner.RunCount >= 1);
        Assert.That(worker.ExecuteTask!.IsFaulted, Is.False,
            () => $"worker faulted: {worker.ExecuteTask.Exception}");
        Assert.That(runner.RunCount, Is.EqualTo(1), () => $"clock={fakeTime.GetUtcNow():O}");

        // While run 1 is in progress, the 00:10 and 00:15 occurrences pass — no new runs.
        fakeTime.Advance(TimeSpan.FromMinutes(11)); // clock now 00:16
        await Task.Delay(100);
        Assert.That(runner.RunCount, Is.EqualTo(1), "no overlapping runs while one is in progress");

        // Finish run 1; the next occurrence is computed from *now* (00:16 → 00:20).
        runner.ReleaseRun();

        // Advance in small steps until the next run fires; it must be a single run,
        // not a burst replaying the missed 00:10/00:15 occurrences.
        for (int i = 0; i < 10 && runner.RunCount < 2; i++)
        {
            fakeTime.Advance(TimeSpan.FromMinutes(1));
            await Task.Delay(25);
        }

        await WaitUntilAsync(() => runner.RunCount >= 2);
        Assert.That(runner.RunCount, Is.EqualTo(2), "missed occurrences must be skipped, not replayed");

        runner.ReleaseRun();
        await worker.StopAsync(CancellationToken.None);
    }

    [Test]
    public async Task RunEvery_deferred_start_waits_one_interval_before_first_run()
    {
        var fakeTime = new FakeTimeProvider(new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var runner = new RecordingJobRunner();
        var registration = new JobRegistration("interval-job").RunEvery(TimeSpan.FromMinutes(10), runImmediately: false);
        var worker = new NBatchJobWorkerService(runner, "interval-job", registration, NullLogger.Instance, fakeTime);

        await worker.StartAsync(CancellationToken.None);

        await Task.Delay(100);
        Assert.That(runner.RunCount, Is.EqualTo(0), "deferred start must not run immediately");

        fakeTime.Advance(TimeSpan.FromMinutes(10));
        await WaitUntilAsync(() => runner.RunCount >= 1);
        Assert.That(runner.RunCount, Is.EqualTo(1));

        await worker.StopAsync(CancellationToken.None);
    }

    [Test]
    public async Task Cron_worker_stops_cleanly_on_cancellation()
    {
        var fakeTime = new FakeTimeProvider(new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var runner = new RecordingJobRunner();
        var registration = new JobRegistration("cron-job").RunOnCron("0 2 * * *");
        var worker = new NBatchJobWorkerService(runner, "cron-job", registration, NullLogger.Instance, fakeTime);

        await worker.StartAsync(CancellationToken.None);
        await Task.Delay(50);

        // Must stop promptly even though the next occurrence is hours of fake time away.
        await worker.StopAsync(CancellationToken.None);

        Assert.That(runner.RunCount, Is.EqualTo(0));
    }

    #endregion
}
