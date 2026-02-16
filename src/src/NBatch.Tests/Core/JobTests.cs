using NBatch.Core;
using NBatch.Core.Exceptions;
using NBatch.Core.Interfaces;
using NUnit.Framework;

namespace NBatch.Tests.Core;

/// <summary>
/// Tests for <see cref="Job"/> — multi-step orchestration, cancellation,
/// listener callbacks, and builder validation.
/// </summary>
[TestFixture]
internal sealed class JobTests
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

    private sealed class CollectingWriter<T> : IWriter<T>
    {
        public List<T> Written { get; } = [];

        public Task WriteAsync(IEnumerable<T> items, CancellationToken cancellationToken = default)
        {
            Written.AddRange(items);
            return Task.CompletedTask;
        }
    }

    private sealed class FailingReader<T> : IReader<T>
    {
        public Task<IEnumerable<T>> ReadAsync(long startIndex, int chunkSize, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Reader failed");
    }

    private sealed class RecordingJobListener : IJobListener
    {
        public string? BeforeJobName { get; private set; }
        public JobResult? AfterResult { get; private set; }

        public Task BeforeJobAsync(string jobName, CancellationToken cancellationToken)
        {
            BeforeJobName = jobName;
            return Task.CompletedTask;
        }

        public Task AfterJobAsync(JobResult result, CancellationToken cancellationToken)
        {
            AfterResult = result;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingStepListener : IStepListener
    {
        public string? BeforeStepName { get; private set; }
        public StepResult? AfterResult { get; private set; }

        public Task BeforeStepAsync(string stepName, CancellationToken cancellationToken)
        {
            BeforeStepName = stepName;
            return Task.CompletedTask;
        }

        public Task AfterStepAsync(StepResult result, CancellationToken cancellationToken)
        {
            AfterResult = result;
            return Task.CompletedTask;
        }
    }

    #endregion

    #region Success path

    [Test]
    public async Task Single_step_job_returns_success()
    {
        var writer = new CollectingWriter<string>();

        var job = Job.CreateBuilder("single-step")
            .AddStep("s1", step => step
                .ReadFrom(new ListReader<string>(["a", "b"]))
                .WriteTo(writer)
                .WithChunkSize(10))
            .Build();

        var result = await job.RunAsync();

        Assert.That(result.Success, Is.True);
        Assert.That(result.Name, Is.EqualTo("single-step"));
        Assert.That(result.Steps, Has.Count.EqualTo(1));
        Assert.That(writer.Written, Is.EqualTo(new[] { "a", "b" }));
    }

    [Test]
    public async Task Multi_step_job_executes_all_steps_in_order()
    {
        var writer1 = new CollectingWriter<string>();
        var writer2 = new CollectingWriter<int>();

        var job = Job.CreateBuilder("multi-step")
            .AddStep("strings", step => step
                .ReadFrom(new ListReader<string>(["x", "y"]))
                .WriteTo(writer1))
            .AddStep("numbers", step => step
                .ReadFrom(new ListReader<int>([1, 2, 3]))
                .WriteTo(writer2))
            .Build();

        var result = await job.RunAsync();

        Assert.That(result.Success, Is.True);
        Assert.That(result.Steps, Has.Count.EqualTo(2));
        Assert.That(writer1.Written, Is.EqualTo(new[] { "x", "y" }));
        Assert.That(writer2.Written, Is.EqualTo(new[] { 1, 2, 3 }));
    }

    [Test]
    public async Task Tasklet_step_executes_successfully()
    {
        bool executed = false;

        var job = Job.CreateBuilder("tasklet-job")
            .AddStep("work", step => step.Execute(() => { executed = true; }))
            .Build();

        var result = await job.RunAsync();

        Assert.That(result.Success, Is.True);
        Assert.That(executed, Is.True);
    }

    #endregion

    #region Failure path — early step fails, later steps don't run

    [Test]
    public async Task When_first_step_fails_second_step_does_not_run()
    {
        var writer = new CollectingWriter<int>();

        var job = Job.CreateBuilder("fail-early")
            .AddStep("failing", step => step
                .ReadFrom(new FailingReader<string>())
                .WriteTo(new CollectingWriter<string>()))
            .AddStep("should-not-run", step => step
                .ReadFrom(new ListReader<int>([1, 2]))
                .WriteTo(writer))
            .Build();

        var result = await job.RunAsync();

        Assert.That(result.Success, Is.False);
        Assert.That(result.Steps, Has.Count.EqualTo(1));
        Assert.That(result.Steps[0].Name, Is.EqualTo("failing"));
        Assert.That(result.Steps[0].Success, Is.False);
        Assert.That(writer.Written, Is.Empty);
    }

    [Test]
    public async Task When_middle_step_fails_preceding_steps_are_recorded()
    {
        var writer1 = new CollectingWriter<string>();

        var job = Job.CreateBuilder("mid-fail")
            .AddStep("ok-step", step => step
                .ReadFrom(new ListReader<string>(["a"]))
                .WriteTo(writer1))
            .AddStep("bad-step", step => step
                .ReadFrom(new FailingReader<int>())
                .WriteTo(new CollectingWriter<int>()))
            .AddStep("never-step", step => step
                .Execute(() => { }))
            .Build();

        var result = await job.RunAsync();

        Assert.That(result.Success, Is.False);
        Assert.That(result.Steps, Has.Count.EqualTo(2));
        Assert.That(result.Steps[0].Success, Is.True);
        Assert.That(result.Steps[1].Success, Is.False);
        Assert.That(writer1.Written, Is.EqualTo(new[] { "a" }));
    }

    [Test]
    public async Task Tasklet_step_failure_returns_failed_result()
    {
        var job = Job.CreateBuilder("tasklet-fail")
            .AddStep("fail", step => step
                .Execute(() => throw new InvalidOperationException("boom")))
            .Build();

        var result = await job.RunAsync();

        Assert.That(result.Success, Is.False);
        Assert.That(result.Steps[0].Success, Is.False);
    }

    #endregion

    #region Cancellation

    [Test]
    public void RunAsync_throws_OperationCanceledException_when_token_is_already_cancelled()
    {
        var job = Job.CreateBuilder("cancel-job")
            .AddStep("s1", step => step
                .ReadFrom(new ListReader<string>(["a"]))
                .WriteTo(new CollectingWriter<string>()))
            .Build();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.ThrowsAsync<OperationCanceledException>(() => job.RunAsync(cts.Token));
    }

    [Test]
    public void RunAsync_throws_OperationCanceledException_when_cancelled_between_steps()
    {
        using var cts = new CancellationTokenSource();

        var job = Job.CreateBuilder("cancel-between-steps")
            .AddStep("s1", step => step
                .Execute(() => { cts.Cancel(); }))
            .AddStep("s2", step => step
                .ReadFrom(new ListReader<string>(["a"]))
                .WriteTo(new CollectingWriter<string>()))
            .Build();

        Assert.ThrowsAsync<OperationCanceledException>(() => job.RunAsync(cts.Token));
    }

    #endregion

    #region Listeners

    [Test]
    public async Task Job_listeners_called_on_success()
    {
        var listener = new RecordingJobListener();

        var job = Job.CreateBuilder("listener-ok")
            .WithListener(listener)
            .AddStep("s1", step => step
                .ReadFrom(new ListReader<string>(["a"]))
                .WriteTo(new CollectingWriter<string>()))
            .Build();

        await job.RunAsync();

        Assert.That(listener.BeforeJobName, Is.EqualTo("listener-ok"));
        Assert.That(listener.AfterResult, Is.Not.Null);
        Assert.That(listener.AfterResult!.Success, Is.True);
    }

    [Test]
    public async Task Job_listeners_called_on_failure()
    {
        var listener = new RecordingJobListener();

        var job = Job.CreateBuilder("listener-fail")
            .WithListener(listener)
            .AddStep("s1", step => step
                .ReadFrom(new FailingReader<string>())
                .WriteTo(new CollectingWriter<string>()))
            .Build();

        await job.RunAsync();

        Assert.That(listener.BeforeJobName, Is.EqualTo("listener-fail"));
        Assert.That(listener.AfterResult, Is.Not.Null);
        Assert.That(listener.AfterResult!.Success, Is.False);
    }

    [Test]
    public async Task Step_listeners_called_on_success()
    {
        var listener = new RecordingStepListener();

        var job = Job.CreateBuilder("step-listener")
            .AddStep("s1", step => step
                .ReadFrom(new ListReader<string>(["a"]))
                .WriteTo(new CollectingWriter<string>())
                .WithListener(listener))
            .Build();

        await job.RunAsync();

        Assert.That(listener.BeforeStepName, Is.EqualTo("s1"));
        Assert.That(listener.AfterResult, Is.Not.Null);
        Assert.That(listener.AfterResult!.Success, Is.True);
    }

    [Test]
    public async Task Step_listeners_called_on_failure()
    {
        var listener = new RecordingStepListener();

        var job = Job.CreateBuilder("step-listener-fail")
            .AddStep("s1", step => step
                .ReadFrom(new FailingReader<string>())
                .WriteTo(new CollectingWriter<string>())
                .WithListener(listener))
            .Build();

        await job.RunAsync();

        Assert.That(listener.BeforeStepName, Is.EqualTo("s1"));
        Assert.That(listener.AfterResult, Is.Not.Null);
        Assert.That(listener.AfterResult!.Success, Is.False);
    }

    #endregion

    #region Builder validation

    [Test]
    public void Duplicate_step_names_throw_DuplicateStepNameException()
    {
        Assert.Throws<DuplicateStepNameException>(() =>
        {
            Job.CreateBuilder("dup-job")
                .AddStep("same-name", step => step
                    .ReadFrom(new ListReader<string>(["a"]))
                    .WriteTo(new CollectingWriter<string>()))
                .AddStep("same-name", step => step
                    .ReadFrom(new ListReader<string>(["b"]))
                    .WriteTo(new CollectingWriter<string>()))
                .Build();
        });
    }

    [Test]
    public void WithChunkSize_zero_throws_ArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            Job.CreateBuilder("bad-chunk")
                .AddStep("s1", step => step
                    .ReadFrom(new ListReader<string>(["a"]))
                    .WriteTo(new CollectingWriter<string>())
                    .WithChunkSize(0))
                .Build();
        });
    }

    [Test]
    public void WithChunkSize_negative_throws_ArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            Job.CreateBuilder("bad-chunk")
                .AddStep("s1", step => step
                    .ReadFrom(new ListReader<string>(["a"]))
                    .WriteTo(new CollectingWriter<string>())
                    .WithChunkSize(-5))
                .Build();
        });
    }

    #endregion

    #region StepResult counters

    [Test]
    public async Task StepResult_reports_correct_item_counts()
    {
        var data = new[] { "a", "b", "c", "d", "e" };
        var writer = new CollectingWriter<string>();

        var job = Job.CreateBuilder("counters")
            .AddStep("s1", step => step
                .ReadFrom(new ListReader<string>(data))
                .WriteTo(writer)
                .WithChunkSize(2))
            .Build();

        var result = await job.RunAsync();

        Assert.That(result.Steps[0].ItemsRead, Is.EqualTo(5));
        Assert.That(result.Steps[0].ItemsProcessed, Is.EqualTo(5));
        Assert.That(result.Steps[0].ErrorsSkipped, Is.EqualTo(0));
    }

    [Test]
    public async Task StepResult_reports_correct_skip_count()
    {
        var data = new[] { "a", "b", "c" };
        int callCount = 0;

        var job = Job.CreateBuilder("skip-counters")
            .AddStep("s1", step => step
                .ReadFrom(new ListReader<string>(data))
                .ProcessWith((string s) =>
                {
                    if (++callCount == 2) throw new FormatException("bad item");
                    return s;
                })
                .WriteTo(new CollectingWriter<string>())
                .WithSkipPolicy(SkipPolicy.For<FormatException>(maxSkips: 3))
                .WithChunkSize(1))
            .Build();

        var result = await job.RunAsync();

        Assert.That(result.Success, Is.True);
        Assert.That(result.Steps[0].ErrorsSkipped, Is.EqualTo(1));
    }

    #endregion
}
