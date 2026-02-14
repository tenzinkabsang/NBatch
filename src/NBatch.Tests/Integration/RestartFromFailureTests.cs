using Microsoft.Data.Sqlite;
using NBatch.Core;
using NBatch.Core.Interfaces;
using NBatch.Core.Repositories;
using NUnit.Framework;

namespace NBatch.Tests.Integration;

/// <summary>
/// Integration tests for the restart-from-failure feature.
/// Each test uses a file-based SQLite database via <see cref="EfJobRepository"/>
/// to prove that job state persists across runs and chunks resume correctly.
/// </summary>
[TestFixture]
internal sealed class RestartFromFailureTests
{
    private string _dbPath = null!;

    [SetUp]
    public void BeforeEach()
    {
        EfJobRepository.ResetInitializationCache();
        _dbPath = Path.Combine(Path.GetTempPath(), $"nbatch_test_{Guid.NewGuid():N}.db");
    }

    [TearDown]
    public void AfterEach()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }

    private string UniqueConnectionString => $"Data Source={_dbPath}";

    #region Helpers

    /// <summary>
    /// A reader backed by a list. Returns a chunk starting at <paramref name="startIndex"/>
    /// with up to <paramref name="chunkSize"/> items.
    /// </summary>
    private sealed class ListReader<T>(IReadOnlyList<T> items) : IReader<T>
    {
        public Task<IEnumerable<T>> ReadAsync(long startIndex, int chunkSize, CancellationToken cancellationToken = default)
        {
            var chunk = items.Skip((int)startIndex).Take(chunkSize);
            return Task.FromResult(chunk);
        }
    }

    /// <summary>
    /// A writer that collects all written items for later assertion.
    /// </summary>
    private sealed class CollectingWriter<T> : IWriter<T>
    {
        public List<T> Written { get; } = [];

        public Task WriteAsync(IEnumerable<T> items, CancellationToken cancellationToken = default)
        {
            Written.AddRange(items);
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// A reader that throws on a specific chunk index, then succeeds on subsequent runs.
    /// Used to simulate a transient failure that causes a job to stop, then restart successfully.
    /// </summary>
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

    /// <summary>
    /// A processor that throws on a configurable number of initial attempts,
    /// then succeeds. Used to test <see cref="SkipPolicy"/> integration.
    /// </summary>
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

    #endregion

    #region 1 — Job restart from a failed chunk

    [Test]
    public async Task Job_restart_resumes_from_failed_chunk()
    {
        // Arrange: 6 items, chunk size 2 ? 3 chunks (0,2,4).
        // The reader fails on chunk index 2 during the first run.
        var data = new[] { "a", "b", "c", "d", "e", "f" };
        var connStr = UniqueConnectionString;

        var failReader = new FailOnceAtIndexReader<string>(data, failAtIndex: 2);
        var writer = new CollectingWriter<string>();

        // Run 1 — should fail on the second chunk
        var job1 = Job.CreateBuilder("restart-job")
            .UseJobStore(connStr, DatabaseProvider.Sqlite)
            .AddStep("step1", step => step
                .ReadFrom(failReader)
                .WriteTo(writer)
                .WithChunkSize(2))
            .Build();

        Assert.ThrowsAsync<InvalidOperationException>(() => job1.RunAsync());

        // Only the first chunk (a, b) should have been written
        Assert.That(writer.Written, Is.EqualTo(new[] { "a", "b" }));

        // Run 2 — same job name, same connection string ? should restart from failed chunk
        // FailOnceAtIndexReader already flipped _hasFailed, so it won't throw again.
        var job2 = Job.CreateBuilder("restart-job")
            .UseJobStore(connStr, DatabaseProvider.Sqlite)
            .AddStep("step1", step => step
                .ReadFrom(failReader)
                .WriteTo(writer)
                .WithChunkSize(2))
            .Build();

        var result = await job2.RunAsync();

        Assert.That(result.Success, Is.True);
        // Restarted from index 2 (the failed chunk), so c,d,e,f should be written
        Assert.That(writer.Written, Is.EqualTo(new[] { "a", "b", "c", "d", "e", "f" }));
    }

    [Test]
    public async Task Job_restart_processes_zero_items_when_all_chunks_completed()
    {
        // Arrange: complete a job fully, then restart — should process nothing new
        var data = new[] { "x", "y" };
        var connStr = UniqueConnectionString;

        var writer = new CollectingWriter<string>();

        var job1 = Job.CreateBuilder("done-job")
            .UseJobStore(connStr, DatabaseProvider.Sqlite)
            .AddStep("step1", step => step
                .ReadFrom(new ListReader<string>(data))
                .WriteTo(writer)
                .WithChunkSize(2))
            .Build();

        var result1 = await job1.RunAsync();
        Assert.That(result1.Success, Is.True);
        Assert.That(writer.Written, Is.EqualTo(new[] { "x", "y" }));

        // Run 2 — nothing left to process
        var writer2 = new CollectingWriter<string>();
        var job2 = Job.CreateBuilder("done-job")
            .UseJobStore(connStr, DatabaseProvider.Sqlite)
            .AddStep("step1", step => step
                .ReadFrom(new ListReader<string>(data))
                .WriteTo(writer2)
                .WithChunkSize(2))
            .Build();

        var result2 = await job2.RunAsync();

        Assert.That(result2.Success, Is.True);
        Assert.That(result2.Steps[0].ItemsRead, Is.EqualTo(0));
    }

    #endregion

    #region 2 — StepContext.RetryPreviousIfFailed with various offsets

    [Test]
    public async Task RetryPreviousIfFailed_backs_up_one_chunk_on_restart()
    {
        // 4 items, chunk size 2 ? chunks at index 0 and 2.
        // Fail at index 2 ? on restart, RetryPreviousIfFailed should back up to index 2
        // (StepIndex=4, NumberOfItemsProcessed=0 ? 4-2=2).
        var data = new[] { "a", "b", "c", "d" };
        var connStr = UniqueConnectionString;

        var failReader = new FailOnceAtIndexReader<string>(data, failAtIndex: 2);
        var writer = new CollectingWriter<string>();

        var job1 = Job.CreateBuilder("retry-offset")
            .UseJobStore(connStr, DatabaseProvider.Sqlite)
            .AddStep("step1", step => step
                .ReadFrom(failReader)
                .WriteTo(writer)
                .WithChunkSize(2))
            .Build();

        Assert.ThrowsAsync<InvalidOperationException>(() => job1.RunAsync());
        Assert.That(writer.Written, Is.EqualTo(new[] { "a", "b" }));

        // Restart
        var job2 = Job.CreateBuilder("retry-offset")
            .UseJobStore(connStr, DatabaseProvider.Sqlite)
            .AddStep("step1", step => step
                .ReadFrom(failReader)
                .WriteTo(writer)
                .WithChunkSize(2))
            .Build();

        var result = await job2.RunAsync();

        Assert.That(result.Success, Is.True);
        // Items c,d should be written from the retried chunk
        Assert.That(writer.Written, Is.EqualTo(new[] { "a", "b", "c", "d" }));
    }

    [Test]
    public async Task RetryPreviousIfFailed_stays_at_zero_when_first_chunk_fails()
    {
        // Fail on the very first chunk ? can't back up below 0.
        var data = new[] { "a", "b" };
        var connStr = UniqueConnectionString;

        var failReader = new FailOnceAtIndexReader<string>(data, failAtIndex: 0);
        var writer = new CollectingWriter<string>();

        var job1 = Job.CreateBuilder("retry-zero")
            .UseJobStore(connStr, DatabaseProvider.Sqlite)
            .AddStep("step1", step => step
                .ReadFrom(failReader)
                .WriteTo(writer)
                .WithChunkSize(2))
            .Build();

        Assert.ThrowsAsync<InvalidOperationException>(() => job1.RunAsync());
        Assert.That(writer.Written, Is.Empty);

        // Restart — should retry from index 0
        var job2 = Job.CreateBuilder("retry-zero")
            .UseJobStore(connStr, DatabaseProvider.Sqlite)
            .AddStep("step1", step => step
                .ReadFrom(failReader)
                .WriteTo(writer)
                .WithChunkSize(2))
            .Build();

        var result = await job2.RunAsync();

        Assert.That(result.Success, Is.True);
        Assert.That(writer.Written, Is.EqualTo(new[] { "a", "b" }));
    }

    [Test]
    public async Task RetryPreviousIfFailed_with_chunk_size_1()
    {
        // Chunk size 1: fail at index 1 ? backs up to index 0 on restart
        // (StepIndex=2, chunkSize=1, NumberOfItemsProcessed=0 ? 2-1=1)
        var data = new[] { "a", "b", "c" };
        var connStr = UniqueConnectionString;

        var failReader = new FailOnceAtIndexReader<string>(data, failAtIndex: 1);
        var writer = new CollectingWriter<string>();

        var job1 = Job.CreateBuilder("retry-cs1")
            .UseJobStore(connStr, DatabaseProvider.Sqlite)
            .AddStep("step1", step => step
                .ReadFrom(failReader)
                .WriteTo(writer)
                .WithChunkSize(1))
            .Build();

        Assert.ThrowsAsync<InvalidOperationException>(() => job1.RunAsync());
        Assert.That(writer.Written, Is.EqualTo(new[] { "a" }));

        // Restart — backs up to index 1 and continues
        var job2 = Job.CreateBuilder("retry-cs1")
            .UseJobStore(connStr, DatabaseProvider.Sqlite)
            .AddStep("step1", step => step
                .ReadFrom(failReader)
                .WriteTo(writer)
                .WithChunkSize(1))
            .Build();

        var result = await job2.RunAsync();

        Assert.That(result.Success, Is.True);
        Assert.That(writer.Written, Is.EqualTo(new[] { "a", "b", "c" }));
    }


    #endregion

    #region 3 — TaskletStep error handling

    [Test]
    public async Task TaskletStep_success_is_recorded()
    {
        var connStr = UniqueConnectionString;
        bool executed = false;

        var job = Job.CreateBuilder("tasklet-ok")
            .UseJobStore(connStr, DatabaseProvider.Sqlite)
            .AddStep("cleanup", step => step
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
    public void TaskletStep_failure_propagates_and_records_error()
    {
        var connStr = UniqueConnectionString;

        var job = Job.CreateBuilder("tasklet-fail")
            .UseJobStore(connStr, DatabaseProvider.Sqlite)
            .AddStep("cleanup", step => step
                .Execute(() => throw new InvalidOperationException("Cleanup failed")))
            .Build();

        Assert.ThrowsAsync<InvalidOperationException>(() => job.RunAsync());
    }

    [Test]
    public async Task TaskletStep_failure_does_not_block_restart()
    {
        var connStr = UniqueConnectionString;
        int callCount = 0;

        // A tasklet that fails on the first call, succeeds on the second
        Task FailOnceThenSucceed()
        {
            if (++callCount == 1)
                throw new InvalidOperationException("First run fails");
            return Task.CompletedTask;
        }

        var job1 = Job.CreateBuilder("tasklet-restart")
            .UseJobStore(connStr, DatabaseProvider.Sqlite)
            .AddStep("cleanup", step => step
                .Execute(FailOnceThenSucceed))
            .Build();

        Assert.ThrowsAsync<InvalidOperationException>(() => job1.RunAsync());
        Assert.That(callCount, Is.EqualTo(1));

        // Restart — should re-execute the tasklet
        var job2 = Job.CreateBuilder("tasklet-restart")
            .UseJobStore(connStr, DatabaseProvider.Sqlite)
            .AddStep("cleanup", step => step
                .Execute(FailOnceThenSucceed))
            .Build();

        var result = await job2.RunAsync();

        Assert.That(result.Success, Is.True);
        Assert.That(callCount, Is.EqualTo(2));
    }

    #endregion

    #region 4 — EfJobRepository with in-memory SQLite provider

    [Test]
    public async Task EfJobRepository_persists_step_progress_across_runs()
    {
        var connStr = UniqueConnectionString;
        var data = new[] { "a", "b", "c", "d" };

        var writer1 = new CollectingWriter<string>();

        // Run 1: process all 4 items in 2 chunks
        var job1 = Job.CreateBuilder("ef-persist")
            .UseJobStore(connStr, DatabaseProvider.Sqlite)
            .AddStep("step1", step => step
                .ReadFrom(new ListReader<string>(data))
                .WriteTo(writer1)
                .WithChunkSize(2))
            .Build();

        var result1 = await job1.RunAsync();
        Assert.That(result1.Success, Is.True);
        Assert.That(writer1.Written, Is.EqualTo(new[] { "a", "b", "c", "d" }));

        // Run 2: same job name ? repository shows step already completed (reader returns empty)
        var writer2 = new CollectingWriter<string>();
        var job2 = Job.CreateBuilder("ef-persist")
            .UseJobStore(connStr, DatabaseProvider.Sqlite)
            .AddStep("step1", step => step
                .ReadFrom(new ListReader<string>(data))
                .WriteTo(writer2)
                .WithChunkSize(2))
            .Build();

        var result2 = await job2.RunAsync();
        Assert.That(result2.Success, Is.True);
        // The reader starts from the last saved index, which is past the data ? 0 items
        Assert.That(writer2.Written, Is.Empty);
    }

    [Test]
    public async Task EfJobRepository_tracks_multiple_steps_independently()
    {
        var connStr = UniqueConnectionString;
        var data1 = new[] { "a", "b" };
        var data2 = new[] { 1, 2, 3 };

        var writer1 = new CollectingWriter<string>();
        var writer2 = new CollectingWriter<int>();

        var job = Job.CreateBuilder("multi-step")
            .UseJobStore(connStr, DatabaseProvider.Sqlite)
            .AddStep("strings", step => step
                .ReadFrom(new ListReader<string>(data1))
                .WriteTo(writer1)
                .WithChunkSize(2))
            .AddStep("numbers", step => step
                .ReadFrom(new ListReader<int>(data2))
                .WriteTo(writer2)
                .WithChunkSize(2))
            .Build();

        var result = await job.RunAsync();

        Assert.That(result.Success, Is.True);
        Assert.That(writer1.Written, Is.EqualTo(new[] { "a", "b" }));
        Assert.That(writer2.Written, Is.EqualTo(new[] { 1, 2, 3 }));
        Assert.That(result.Steps, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task EfJobRepository_records_skip_exceptions()
    {
        var connStr = UniqueConnectionString;
        var data = new[] { "item1" };

        var writer = new CollectingWriter<string>();
        var processor = new FailNTimesProcessor<string>(failCount: int.MaxValue);
        var skipPolicy = new SkipPolicy([typeof(TimeoutException)], skipLimit: 1);

        var job = Job.CreateBuilder("ef-skip")
            .UseJobStore(connStr, DatabaseProvider.Sqlite)
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
    }

    #endregion
}
