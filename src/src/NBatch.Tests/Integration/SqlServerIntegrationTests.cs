using NBatch.Core;
using NBatch.Core.Interfaces;
using NBatch.Core.Repositories;
using NUnit.Framework;

namespace NBatch.Tests.Integration;

/// <summary>
/// Integration tests that run against a real SQL Server instance via docker-compose.
///
/// Prerequisites:
///   cd src &amp;&amp; docker-compose up -d
///
/// Run these tests with:
///   dotnet test --filter "Category=Docker"
/// </summary>
[TestFixture]
[Category("Docker")]
internal sealed class SqlServerIntegrationTests
{
    private const string ConnectionString =
        "Server=localhost,1433;Database=NBatch_IntegrationTests;User Id=sa;Password=@Password1234;TrustServerCertificate=True;";

    [SetUp]
    public void BeforeEach()
    {
        EfJobRepository.ResetInitializationCache();
    }

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

    private sealed class FailOnceAtIndexReader<T>(IReadOnlyList<T> items, long failAtIndex) : IReader<T>
    {
        private bool _hasFailed;

        public Task<IEnumerable<T>> ReadAsync(long startIndex, int chunkSize, CancellationToken cancellationToken = default)
        {
            if (!_hasFailed && startIndex == failAtIndex)
            {
                _hasFailed = true;
                throw new InvalidOperationException($"Simulated failure at index {failAtIndex}");
            }

            var chunk = items.Skip((int)startIndex).Take(chunkSize);
            return Task.FromResult(chunk);
        }
    }

    private sealed class FailNTimesProcessor<T>(int failCount) : IProcessor<T, T>
    {
        private int _calls;

        public Task<T> ProcessAsync(T input, CancellationToken cancellationToken = default)
        {
            if (++_calls <= failCount)
                throw new TimeoutException($"Transient failure #{_calls}");
            return Task.FromResult(input);
        }
    }

    /// <summary>Generates a unique job name per test to avoid cross-test interference.</summary>
    private static string UniqueJobName([System.Runtime.CompilerServices.CallerMemberName] string caller = "")
        => $"{caller}_{Guid.NewGuid():N}";

    #endregion

    #region 1 — Basic chunk processing with SQL Server

    [Test]
    public async Task Job_processes_all_chunks_with_SqlServer()
    {
        var data = new[] { "a", "b", "c", "d", "e" };
        var writer = new CollectingWriter<string>();

        var job = Job.CreateBuilder(UniqueJobName())
            .UseJobStore(ConnectionString, DatabaseProvider.SqlServer)
            .AddStep("step1", step => step
                .ReadFrom(new ListReader<string>(data))
                .WriteTo(writer)
                .WithChunkSize(2))
            .Build();

        var result = await job.RunAsync();

        Assert.That(result.Success, Is.True);
        Assert.That(writer.Written, Is.EqualTo(new[] { "a", "b", "c", "d", "e" }));
        Assert.That(result.Steps[0].ItemsRead, Is.EqualTo(5));
        Assert.That(result.Steps[0].ItemsProcessed, Is.EqualTo(5));
    }

    #endregion

    #region 2 — Restart from failure with SQL Server

    [Test]
    public async Task Job_restart_resumes_from_failed_chunk_with_SqlServer()
    {
        var data = new[] { "a", "b", "c", "d" };
        var jobName = UniqueJobName();

        var failReader = new FailOnceAtIndexReader<string>(data, failAtIndex: 2);
        var writer = new CollectingWriter<string>();

        var job1 = Job.CreateBuilder(jobName)
            .UseJobStore(ConnectionString, DatabaseProvider.SqlServer)
            .AddStep("step1", step => step
                .ReadFrom(failReader)
                .WriteTo(writer)
                .WithChunkSize(2))
            .Build();

        var result1 = await job1.RunAsync();
        Assert.That(result1.Success, Is.False);
        Assert.That(writer.Written, Is.EqualTo(new[] { "a", "b" }));

        // Run 2 — should restart from the failed chunk
        var job2 = Job.CreateBuilder(jobName)
            .UseJobStore(ConnectionString, DatabaseProvider.SqlServer)
            .AddStep("step1", step => step
                .ReadFrom(failReader)
                .WriteTo(writer)
                .WithChunkSize(2))
            .Build();

        var result2 = await job2.RunAsync();

        Assert.That(result2.Success, Is.True);
        Assert.That(writer.Written, Is.EqualTo(new[] { "a", "b", "c", "d" }));
    }

    #endregion

    #region 3 — Multi-step job with SQL Server

    [Test]
    public async Task Multi_step_job_tracks_steps_independently_with_SqlServer()
    {
        var writer1 = new CollectingWriter<string>();
        var writer2 = new CollectingWriter<int>();

        var job = Job.CreateBuilder(UniqueJobName())
            .UseJobStore(ConnectionString, DatabaseProvider.SqlServer)
            .AddStep("strings", step => step
                .ReadFrom(new ListReader<string>(["x", "y"]))
                .WriteTo(writer1)
                .WithChunkSize(2))
            .AddStep("numbers", step => step
                .ReadFrom(new ListReader<int>([1, 2, 3]))
                .WriteTo(writer2)
                .WithChunkSize(2))
            .Build();

        var result = await job.RunAsync();

        Assert.That(result.Success, Is.True);
        Assert.That(result.Steps, Has.Count.EqualTo(2));
        Assert.That(writer1.Written, Is.EqualTo(new[] { "x", "y" }));
        Assert.That(writer2.Written, Is.EqualTo(new[] { 1, 2, 3 }));
    }

    #endregion

    #region 4 — Skip policy with SQL Server

    [Test]
    public async Task Skip_policy_works_with_SqlServer()
    {
        var data = new[] { "a", "b" };
        var processor = new FailNTimesProcessor<string>(failCount: 1);
        var skipPolicy = new SkipPolicy([typeof(TimeoutException)], skipLimit: 2);
        var writer = new CollectingWriter<string>();

        var job = Job.CreateBuilder(UniqueJobName())
            .UseJobStore(ConnectionString, DatabaseProvider.SqlServer)
            .AddStep("step1", step => step
                .ReadFrom(new ListReader<string>(data))
                .ProcessWith(processor)
                .WriteTo(writer)
                .WithSkipPolicy(skipPolicy)
                .WithChunkSize(1))
            .Build();

        var result = await job.RunAsync();

        Assert.That(result.Success, Is.True);
        Assert.That(result.Steps[0].ErrorsSkipped, Is.EqualTo(1));
        Assert.That(writer.Written, Is.EqualTo(new[] { "b" }));
    }

    [Test]
    public async Task Skip_budget_resets_per_execution_with_SqlServer()
    {
        var jobName = UniqueJobName();
        var skipPolicy = new SkipPolicy([typeof(TimeoutException)], skipLimit: 2);

        // Run 1: 2 items, all fail and are skipped (2 of 2 budget used). Job succeeds.
        var job1 = Job.CreateBuilder(jobName)
            .UseJobStore(ConnectionString, DatabaseProvider.SqlServer)
            .AddStep("step1", step => step
                .ReadFrom(new ListReader<string>(["a", "b"]))
                .ProcessWith(new FailNTimesProcessor<string>(failCount: int.MaxValue))
                .WriteTo(new CollectingWriter<string>())
                .WithSkipPolicy(skipPolicy)
                .WithChunkSize(1))
            .Build();

        var result1 = await job1.RunAsync();
        Assert.That(result1.Success, Is.True);
        Assert.That(result1.Steps[0].ErrorsSkipped, Is.EqualTo(2));

        // Run 2: same job name. The reader has 4 items so indices 2–3 are new data.
        // If the skip budget were global, we'd have 0 remaining and the first failure would be fatal.
        var job2 = Job.CreateBuilder(jobName)
            .UseJobStore(ConnectionString, DatabaseProvider.SqlServer)
            .AddStep("step1", step => step
                .ReadFrom(new ListReader<string>(["a", "b", "c", "d"]))
                .ProcessWith(new FailNTimesProcessor<string>(failCount: int.MaxValue))
                .WriteTo(new CollectingWriter<string>())
                .WithSkipPolicy(skipPolicy)
                .WithChunkSize(1))
            .Build();

        var result2 = await job2.RunAsync();

        // Budget reset ? 2 new items skipped successfully.
        Assert.That(result2.Success, Is.True);
        Assert.That(result2.Steps[0].ErrorsSkipped, Is.EqualTo(2));
    }

    #endregion

    #region 5 — Tasklet step with SQL Server

    [Test]
    public async Task Tasklet_step_success_recorded_with_SqlServer()
    {
        bool executed = false;

        var job = Job.CreateBuilder(UniqueJobName())
            .UseJobStore(ConnectionString, DatabaseProvider.SqlServer)
            .AddStep("tasklet", step => step
                .Execute(() =>
                {
                    executed = true;
                    return Task.CompletedTask;
                }))
            .Build();

        var result = await job.RunAsync();

        Assert.That(result.Success, Is.True);
        Assert.That(executed, Is.True);
    }

    [Test]
    public async Task Tasklet_step_failure_recorded_with_SqlServer()
    {
        var job = Job.CreateBuilder(UniqueJobName())
            .UseJobStore(ConnectionString, DatabaseProvider.SqlServer)
            .AddStep("tasklet", step => step
                .Execute(() => throw new InvalidOperationException("boom")))
            .Build();

        var result = await job.RunAsync();

        Assert.That(result.Success, Is.False);
    }

    #endregion

    #region 6 — Completed job produces zero items on re-run

    [Test]
    public async Task Completed_job_produces_zero_items_on_rerun_with_SqlServer()
    {
        var data = new[] { "a", "b", "c" };
        var jobName = UniqueJobName();

        var writer1 = new CollectingWriter<string>();
        var job1 = Job.CreateBuilder(jobName)
            .UseJobStore(ConnectionString, DatabaseProvider.SqlServer)
            .AddStep("step1", step => step
                .ReadFrom(new ListReader<string>(data))
                .WriteTo(writer1)
                .WithChunkSize(2))
            .Build();

        var result1 = await job1.RunAsync();
        Assert.That(result1.Success, Is.True);
        Assert.That(writer1.Written, Is.EqualTo(new[] { "a", "b", "c" }));

        // Run 2: same job name ? should resume past all data.
        var writer2 = new CollectingWriter<string>();
        var job2 = Job.CreateBuilder(jobName)
            .UseJobStore(ConnectionString, DatabaseProvider.SqlServer)
            .AddStep("step1", step => step
                .ReadFrom(new ListReader<string>(data))
                .WriteTo(writer2)
                .WithChunkSize(2))
            .Build();

        var result2 = await job2.RunAsync();
        Assert.That(result2.Success, Is.True);
        Assert.That(result2.Steps[0].ItemsRead, Is.EqualTo(0));
        Assert.That(writer2.Written, Is.Empty);
    }

    #endregion
}
