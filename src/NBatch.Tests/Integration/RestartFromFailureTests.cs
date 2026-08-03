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
        // Arrange: 6 items, chunk size 2 → 3 chunks (0,2,4).
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

        var result1 = await job1.RunAsync();
        Assert.That(result1.Success, Is.False);

        // Only the first chunk (a, b) should have been written
        Assert.That(writer.Written, Is.EqualTo(new[] { "a", "b" }));

        // Run 2 — same job name, same connection string → should restart from failed chunk
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
    public async Task Job_rerun_after_success_auto_resets_and_reprocesses()
    {
        // Arrange: complete a job fully, then run it again — the job store resets
        // after a successful run, so the second run reprocesses from the beginning.
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

        // Run 2 — starts fresh from index 0
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
        Assert.That(result2.Steps[0].ItemsRead, Is.EqualTo(2));
        Assert.That(writer2.Written, Is.EqualTo(new[] { "x", "y" }));
    }

    #endregion

    #region 2 — StepContext.RetryPreviousIfFailed with various offsets

    [Test]
    public async Task RetryPreviousIfFailed_backs_up_one_chunk_on_restart()
    {
        // 4 items, chunk size 2 → chunks at index 0 and 2.
        // Fail at index 2 → on restart, RetryPreviousIfFailed should back up to index 2
        // (StepIndex=4, NumberOfItemsProcessed=0 → 4-2=2).
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

        var result1 = await job1.RunAsync();
        Assert.That(result1.Success, Is.False);
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
        // Fail on the very first chunk → can't back up below 0.
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

        var result1 = await job1.RunAsync();
        Assert.That(result1.Success, Is.False);
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
        // Chunk size 1: fail at index 1 → backs up to index 0 on restart
        // (StepIndex=2, chunkSize=1, NumberOfItemsProcessed=0 → 2-1=1)
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

        var result1 = await job1.RunAsync();
        Assert.That(result1.Success, Is.False);
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
    public async Task TaskletStep_failure_propagates_and_records_error()
    {
        var connStr = UniqueConnectionString;

        var job = Job.CreateBuilder("tasklet-fail")
            .UseJobStore(connStr, DatabaseProvider.Sqlite)
            .AddStep("cleanup", step => step
                .Execute(() => throw new InvalidOperationException("Cleanup failed")))
            .Build();

        var result = await job.RunAsync();
        Assert.That(result.Success, Is.False);
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

        var result1 = await job1.RunAsync();
        Assert.That(result1.Success, Is.False);
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
        // Progress must survive across independent repository instances (as it would
        // across process restarts). Run 1 fails mid-way; a brand-new builder + repository
        // against the same database resumes instead of starting over.
        var connStr = UniqueConnectionString;
        var data = new[] { "a", "b", "c", "d" };

        var failReader = new FailOnceAtIndexReader<string>(data, failAtIndex: 2);
        var writer1 = new CollectingWriter<string>();

        var job1 = Job.CreateBuilder("ef-persist")
            .UseJobStore(connStr, DatabaseProvider.Sqlite)
            .AddStep("step1", step => step
                .ReadFrom(failReader)
                .WriteTo(writer1)
                .WithChunkSize(2))
            .Build();

        var result1 = await job1.RunAsync();
        Assert.That(result1.Success, Is.False);
        Assert.That(writer1.Written, Is.EqualTo(new[] { "a", "b" }));

        // Run 2: fresh Job + EfJobRepository instances, same database → resumes at index 2
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
        Assert.That(writer2.Written, Is.EqualTo(new[] { "c", "d" }));
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
        Assert.That(result.Steps[0].ItemsSkipped, Is.EqualTo(1));
    }

    #endregion

    #region 5 — Skip budget resets per execution

    [Test]
    public async Task Skip_budget_resets_on_new_run()
    {
        // Run 1: 3 items, processor fails on every item, skip limit 3 → all 3 skipped, job succeeds.
        // Run 2: different job name to start fresh. Same skip limit → budget should be 3 (not 0 left from run 1).
        // This proves exception counts are scoped per execution, not global.
        var connStr = UniqueConnectionString;
        var data = new[] { "a", "b", "c" };
        var skipPolicy = new SkipPolicy([typeof(TimeoutException)], skipLimit: 3);

        var job1 = Job.CreateBuilder("skip-reset-run1")
            .UseJobStore(connStr, DatabaseProvider.Sqlite)
            .AddStep("step1", step => step
                .ReadFrom(new ListReader<string>(data))
                .ProcessWith(new FailNTimesProcessor<string>(failCount: int.MaxValue))
                .WriteTo(new CollectingWriter<string>())
                .WithSkipPolicy(skipPolicy)
                .WithChunkSize(1))
            .Build();

        var result1 = await job1.RunAsync();

        Assert.That(result1.Success, Is.True);
        Assert.That(result1.Steps[0].ItemsSkipped, Is.EqualTo(3));

        // Run 2: fresh job name, same DB → proves budget is per-execution, not shared across the DB.
        var job2 = Job.CreateBuilder("skip-reset-run2")
            .UseJobStore(connStr, DatabaseProvider.Sqlite)
            .AddStep("step1", step => step
                .ReadFrom(new ListReader<string>(data))
                .ProcessWith(new FailNTimesProcessor<string>(failCount: int.MaxValue))
                .WriteTo(new CollectingWriter<string>())
                .WithSkipPolicy(skipPolicy)
                .WithChunkSize(1))
            .Build();

        var result2 = await job2.RunAsync();

        // Skip budget should be independent — all 3 skipped again.
        Assert.That(result2.Success, Is.True);
        Assert.That(result2.Steps[0].ItemsSkipped, Is.EqualTo(3));
    }

    [Test]
    public async Task Skip_limit_exceeded_fails_the_step()
    {
        var connStr = UniqueConnectionString;
        var data = new[] { "a", "b", "c", "d" };

        // Processor fails on every item. Skip limit is 2, but we have 4 items → third failure exceeds limit.
        var processor = new FailNTimesProcessor<string>(failCount: int.MaxValue);
        var skipPolicy = new SkipPolicy([typeof(TimeoutException)], skipLimit: 2);

        var job = Job.CreateBuilder("skip-exceed")
            .UseJobStore(connStr, DatabaseProvider.Sqlite)
            .AddStep("step1", step => step
                .ReadFrom(new ListReader<string>(data))
                .ProcessWith(processor)
                .WriteTo(new CollectingWriter<string>())
                .WithSkipPolicy(skipPolicy)
                .WithChunkSize(1))
            .Build();

        var result = await job.RunAsync();

        Assert.That(result.Success, Is.False);
    }

    [Test]
    public async Task Non_matching_exception_type_is_not_skipped()
    {
        var connStr = UniqueConnectionString;
        var data = new[] { "a" };

        // Skip policy is for TimeoutException, but processor throws InvalidOperationException.
        var job = Job.CreateBuilder("skip-mismatch")
            .UseJobStore(connStr, DatabaseProvider.Sqlite)
            .AddStep("step1", step => step
                .ReadFrom(new ListReader<string>(data))
                .ProcessWith<string>((string s) => throw new InvalidOperationException("wrong type"))
                .WriteTo(new CollectingWriter<string>())
                .WithSkipPolicy(SkipPolicy.For<TimeoutException>(maxSkips: 10))
                .WithChunkSize(1))
            .Build();

        var result = await job.RunAsync();

        Assert.That(result.Success, Is.False);
    }

    #endregion

    #region 6 — Skipped chunk does not trigger retry on restart

    [Test]
    public async Task Skipped_chunk_does_not_cause_backup_on_restart()
    {
        // Run 1: step1 skips item "b" and SUCCEEDS, but a later tasklet step fails,
        // so the job fails and the next run resumes (no auto-reset).
        // On resume, step1 must NOT back up to re-process the skipped index —
        // a skip is final, only errors trigger backup.
        var connStr = UniqueConnectionString;
        var data = new[] { "a", "b", "c" };

        var job1 = Job.CreateBuilder("skip-no-backup")
            .UseJobStore(connStr, DatabaseProvider.Sqlite)
            .AddStep("step1", step => step
                .ReadFrom(new ListReader<string>(data))
                .ProcessWith((string s) =>
                {
                    if (s == "b") throw new FormatException("bad item");
                    return s;
                })
                .WriteTo(new CollectingWriter<string>())
                .WithSkipPolicy(SkipPolicy.For<FormatException>(maxSkips: 5))
                .WithChunkSize(1))
            .AddStep("flaky-tasklet", step => step
                .Execute(() => throw new InvalidOperationException("first run fails")))
            .Build();

        var result1 = await job1.RunAsync();
        Assert.That(result1.Success, Is.False);
        Assert.That(result1.Steps[0].Success, Is.True);
        Assert.That(result1.Steps[0].ItemsSkipped, Is.EqualTo(1));

        // Run 2: resume. step1 starts past all previously processed data (skip is not retried).
        var writer2 = new CollectingWriter<string>();

        var job2 = Job.CreateBuilder("skip-no-backup")
            .UseJobStore(connStr, DatabaseProvider.Sqlite)
            .AddStep("step1", step => step
                .ReadFrom(new ListReader<string>(data))
                .WriteTo(writer2)
                .WithChunkSize(1))
            .AddStep("flaky-tasklet", step => step
                .Execute(() => { }))
            .Build();

        var result2 = await job2.RunAsync();

        Assert.That(result2.Success, Is.True);
        // All step1 data was handled in run 1 (2 written + 1 skipped) — nothing new to process.
        Assert.That(result2.Steps[0].ItemsRead, Is.EqualTo(0));
        Assert.That(writer2.Written, Is.Empty);
    }

    #endregion

    #region 7 — Auto-reset, tasklet resume, builder ordering, schema migration

    [Test]
    public async Task RunEvery_style_repeated_runs_reprocess_each_time()
    {
        // Three consecutive successful runs of the same job against the same store:
        // each run auto-resets and reprocesses the full data set.
        var connStr = UniqueConnectionString;
        var data = new[] { "a", "b", "c" };
        var writer = new CollectingWriter<string>();

        for (int run = 1; run <= 3; run++)
        {
            var job = Job.CreateBuilder("repeat-job")
                .UseJobStore(connStr, DatabaseProvider.Sqlite)
                .AddStep("step1", step => step
                    .ReadFrom(new ListReader<string>(data))
                    .WriteTo(writer)
                    .WithChunkSize(2))
                .Build();

            var result = await job.RunAsync();
            Assert.That(result.Success, Is.True, $"run {run} should succeed");
            Assert.That(writer.Written, Has.Count.EqualTo(run * data.Length),
                $"run {run} should have reprocessed all {data.Length} items");
        }
    }

    [Test]
    public async Task Tasklet_completed_step_skipped_on_resume_after_later_failure()
    {
        var connStr = UniqueConnectionString;
        int taskletCalls = 0;

        Job BuildJob(bool chunkStepFails)
        {
            var data = new[] { "a", "b" };
            IReader<string> reader = chunkStepFails
                ? new FailOnceAtIndexReader<string>(data, failAtIndex: 0)
                : new ListReader<string>(data);

            return Job.CreateBuilder("tasklet-resume")
                .UseJobStore(connStr, DatabaseProvider.Sqlite)
                .AddStep("notify", step => step
                    .Execute(() => { taskletCalls++; }))
                .AddStep("import", step => step
                    .ReadFrom(reader)
                    .WriteTo(new CollectingWriter<string>())
                    .WithChunkSize(2))
                .Build();
        }

        // Run 1: tasklet succeeds, chunk step fails → job fails.
        var result1 = await BuildJob(chunkStepFails: true).RunAsync();
        Assert.That(result1.Success, Is.False);
        Assert.That(taskletCalls, Is.EqualTo(1));

        // Run 2: resume — the completed tasklet must NOT run again.
        var result2 = await BuildJob(chunkStepFails: false).RunAsync();
        Assert.That(result2.Success, Is.True);
        Assert.That(taskletCalls, Is.EqualTo(1), "completed tasklet must be skipped on resume");
    }

    [Test]
    public async Task Tasklet_reruns_after_successful_job_reset()
    {
        var connStr = UniqueConnectionString;
        int taskletCalls = 0;

        Job BuildJob() => Job.CreateBuilder("tasklet-rerun")
            .UseJobStore(connStr, DatabaseProvider.Sqlite)
            .AddStep("notify", step => step
                .Execute(() => { taskletCalls++; }))
            .Build();

        var result1 = await BuildJob().RunAsync();
        Assert.That(result1.Success, Is.True);
        Assert.That(taskletCalls, Is.EqualTo(1));

        // The first run completed successfully → auto-reset → the tasklet runs again.
        var result2 = await BuildJob().RunAsync();
        Assert.That(result2.Success, Is.True);
        Assert.That(taskletCalls, Is.EqualTo(2));
    }

    [Test]
    public async Task Builder_order_use_job_store_after_add_step_still_persists()
    {
        // Regression guard for the v2 bug where steps registered before UseJobStore
        // silently kept the in-memory repository (no persistence, no restart).
        var connStr = UniqueConnectionString;
        var data = new[] { "a", "b", "c", "d" };

        var failReader = new FailOnceAtIndexReader<string>(data, failAtIndex: 2);
        var writer1 = new CollectingWriter<string>();

        // AddStep BEFORE UseJobStore — must still bind the SQL-backed store.
        var job1 = Job.CreateBuilder("order-independent")
            .AddStep("step1", step => step
                .ReadFrom(failReader)
                .WriteTo(writer1)
                .WithChunkSize(2))
            .UseJobStore(connStr, DatabaseProvider.Sqlite)
            .Build();

        var result1 = await job1.RunAsync();
        Assert.That(result1.Success, Is.False);
        Assert.That(writer1.Written, Is.EqualTo(new[] { "a", "b" }));

        // A new job instance resumes from the persisted index — proving the
        // first run's progress went to SQLite, not to an in-memory store.
        var writer2 = new CollectingWriter<string>();
        var job2 = Job.CreateBuilder("order-independent")
            .AddStep("step1", step => step
                .ReadFrom(new ListReader<string>(data))
                .WriteTo(writer2)
                .WithChunkSize(2))
            .UseJobStore(connStr, DatabaseProvider.Sqlite)
            .Build();

        var result2 = await job2.RunAsync();
        Assert.That(result2.Success, Is.True);
        Assert.That(writer2.Written, Is.EqualTo(new[] { "c", "d" }));
    }

    [Test]
    public async Task Skip_counts_are_per_item_with_chunk_greater_than_one()
    {
        // 6 items, chunk 3, two bad items spread across chunks: only the bad items
        // are skipped — the good items in the same chunks are still written.
        var connStr = UniqueConnectionString;
        var data = new[] { "a", "b", "c", "d", "e", "f" };
        var writer = new CollectingWriter<string>();

        var job = Job.CreateBuilder("per-item-skip")
            .UseJobStore(connStr, DatabaseProvider.Sqlite)
            .AddStep("step1", step => step
                .ReadFrom(new ListReader<string>(data))
                .ProcessWith((string s) =>
                {
                    if (s is "b" or "e") throw new FormatException($"bad item {s}");
                    return s;
                })
                .WriteTo(writer)
                .WithSkipPolicy(SkipPolicy.For<FormatException>(maxSkips: 5))
                .WithChunkSize(3))
            .Build();

        var result = await job.RunAsync();

        Assert.That(result.Success, Is.True);
        Assert.That(result.Steps[0].ItemsSkipped, Is.EqualTo(2));
        Assert.That(writer.Written, Is.EquivalentTo(new[] { "a", "c", "d", "f" }));
    }

    [Test]
    public async Task Cancelled_run_resumes_on_restart_without_losing_the_cancelled_chunk()
    {
        // Run 1 is cancelled while processing the second chunk. The aborted chunk is
        // recorded as an error, so the restart backs up and re-processes it — no items lost.
        var connStr = UniqueConnectionString;
        var data = new[] { "a", "b", "c", "d", "e", "f" };
        var writer1 = new CollectingWriter<string>();

        var job1 = Job.CreateBuilder("cancel-resume")
            .UseJobStore(connStr, DatabaseProvider.Sqlite)
            .AddStep("step1", step => step
                .ReadFrom(new ListReader<string>(data))
                .ProcessWith((string s) =>
                {
                    if (s == "c") throw new OperationCanceledException("cooperative cancel");
                    return s;
                })
                .WriteTo(writer1)
                .WithChunkSize(2))
            .Build();

        Assert.ThrowsAsync<OperationCanceledException>(() => job1.RunAsync());
        Assert.That(writer1.Written, Is.EqualTo(new[] { "a", "b" }));

        // Run 2 resumes from the aborted chunk (no auto-reset after a cancelled run).
        var writer2 = new CollectingWriter<string>();
        var job2 = Job.CreateBuilder("cancel-resume")
            .UseJobStore(connStr, DatabaseProvider.Sqlite)
            .AddStep("step1", step => step
                .ReadFrom(new ListReader<string>(data))
                .WriteTo(writer2)
                .WithChunkSize(2))
            .Build();

        var result2 = await job2.RunAsync();

        Assert.That(result2.Success, Is.True);
        Assert.That(writer2.Written, Is.EqualTo(new[] { "c", "d", "e", "f" }));
    }

    [Test]
    public async Task V2_schema_upgrades_automatically_on_sqlite()
    {
        // Hand-create a database with the v2 schema (no last_run_success column),
        // then run a job: the automatic migration must add the column and the run succeed.
        var connStr = UniqueConnectionString;

        await using (var connection = new SqliteConnection(connStr))
        {
            await connection.OpenAsync();
            var create = connection.CreateCommand();
            create.CommandText = """
                CREATE TABLE "jobs" (
                    "job_name" TEXT NOT NULL CONSTRAINT "PK_jobs" PRIMARY KEY,
                    "create_date" TEXT NOT NULL,
                    "last_run" TEXT NOT NULL
                );
                CREATE TABLE "steps" (
                    "id" INTEGER NOT NULL CONSTRAINT "PK_steps" PRIMARY KEY AUTOINCREMENT,
                    "step_name" TEXT NOT NULL,
                    "job_name" TEXT NOT NULL,
                    "step_index" INTEGER NOT NULL,
                    "number_of_items_processed" INTEGER NOT NULL,
                    "error" INTEGER NOT NULL DEFAULT 0,
                    "skipped" INTEGER NOT NULL DEFAULT 0,
                    "run_date" TEXT NOT NULL,
                    CONSTRAINT "FK_steps_jobs_job_name" FOREIGN KEY ("job_name") REFERENCES "jobs" ("job_name")
                );
                CREATE TABLE "step_exceptions" (
                    "id" INTEGER NOT NULL CONSTRAINT "PK_step_exceptions" PRIMARY KEY AUTOINCREMENT,
                    "step_index" INTEGER NOT NULL,
                    "step_name" TEXT NOT NULL,
                    "job_name" TEXT NOT NULL,
                    "execution_id" INTEGER NOT NULL,
                    "exception_msg" TEXT NULL,
                    "exception_details" TEXT NULL,
                    "create_date" TEXT NOT NULL,
                    CONSTRAINT "FK_step_exceptions_jobs_job_name" FOREIGN KEY ("job_name") REFERENCES "jobs" ("job_name")
                );
                """;
            await create.ExecuteNonQueryAsync();
        }

        var writer = new CollectingWriter<string>();
        var job = Job.CreateBuilder("v2-upgrade")
            .UseJobStore(connStr, DatabaseProvider.Sqlite)
            .AddStep("step1", step => step
                .ReadFrom(new ListReader<string>(["a", "b"]))
                .WriteTo(writer)
                .WithChunkSize(2))
            .Build();

        var result = await job.RunAsync();
        Assert.That(result.Success, Is.True);
        Assert.That(writer.Written, Is.EqualTo(new[] { "a", "b" }));

        // The migration must have added the column.
        await using (var connection = new SqliteConnection(connStr))
        {
            await connection.OpenAsync();
            var check = connection.CreateCommand();
            check.CommandText = "SELECT COUNT(*) FROM pragma_table_info('jobs') WHERE name = 'last_run_success'";
            var columnCount = (long)(await check.ExecuteScalarAsync())!;
            Assert.That(columnCount, Is.EqualTo(1), "last_run_success column should have been added");
        }
    }

    #endregion
}
