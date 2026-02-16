using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
}
