using Microsoft.EntityFrameworkCore;
using NBatch.Core;
using NBatch.Core.Interfaces;
using NBatch.Core.Repositories;
using NBatch.Readers.DbReader;
using NBatch.Tests.Integration.Fixtures;
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

    /// <summary>
    /// A processor that fails after a configurable number of successful items.
    /// Used to simulate mid-dataset failures for restart tests.
    /// </summary>
    private sealed class FailAfterNItemsProcessor<T>(int succeedCount) : IProcessor<T, T>
    {
        private int _processed;

        public Task<T> ProcessAsync(T input, CancellationToken cancellationToken = default)
        {
            if (_processed >= succeedCount)
                throw new InvalidOperationException($"Simulated failure after {succeedCount} items");
            _processed++;
            return Task.FromResult(input);
        }
    }

    private static DbContextOptions<TestDbContext> SqlServerOptions
        => TestDbContext.ForSqlServer(ConnectionString);

    private async Task TruncateEtlTableAsync()
    {
        await using var ctx = new TestDbContext(SqlServerOptions);
        await ctx.Database.ExecuteSqlRawAsync("DELETE FROM TestRecordEtl");
    }

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
        var data = new[] { "a", "b" };
        var jobName = UniqueJobName();
        var skipPolicy = new SkipPolicy([typeof(TimeoutException)], skipLimit: 2);

        // Run 1: items at indices 0-1 fail and are skipped (2 of 2 budget used).
        // Step advances to index 2.
        var job1 = Job.CreateBuilder(jobName)
            .UseJobStore(ConnectionString, DatabaseProvider.SqlServer)
            .AddStep("step1", step => step
                .ReadFrom(new ListReader<string>(data))
                .ProcessWith(new FailNTimesProcessor<string>(failCount: int.MaxValue))
                .WriteTo(new CollectingWriter<string>())
                .WithSkipPolicy(skipPolicy)
                .WithChunkSize(1))
            .Build();

        var result1 = await job1.RunAsync();
        Assert.That(result1.Success, Is.True);
        Assert.That(result1.Steps[0].ErrorsSkipped, Is.EqualTo(2));

        // Run 2: budget should reset. Resumes from index 2, items at indices 2-3
        // fail and are skipped using the freshly reset budget.
        var data2 = new[] { "a", "b", "c", "d" };
        var job2 = Job.CreateBuilder(jobName)
            .UseJobStore(ConnectionString, DatabaseProvider.SqlServer)
            .AddStep("step1", step => step
                .ReadFrom(new ListReader<string>(data2))
                .ProcessWith(new FailNTimesProcessor<string>(failCount: int.MaxValue))
                .WriteTo(new CollectingWriter<string>())
                .WithSkipPolicy(skipPolicy)
                .WithChunkSize(1))
            .Build();

        var result2 = await job2.RunAsync();
        Assert.That(result2.Success, Is.True);
        Assert.That(result2.Steps[0].ErrorsSkipped, Is.EqualTo(2));
    }

    [Test]
    public async Task Failed_run_resumes_at_correct_index_with_reset_skip_budget_with_SqlServer()
    {
        var data = new[] { "a", "b", "c", "d" };
        var jobName = UniqueJobName();
        var skipPolicy = new SkipPolicy([typeof(TimeoutException)], skipLimit: 1);

        // Run 1: processor always fails.
        //   index 0 ("a") → skipped (budget 0/1)
        //   index 1 ("b") → budget exhausted → step fails
        var writer1 = new CollectingWriter<string>();
        var job1 = Job.CreateBuilder(jobName)
            .UseJobStore(ConnectionString, DatabaseProvider.SqlServer)
            .AddStep("step1", step => step
                .ReadFrom(new ListReader<string>(data))
                .ProcessWith(new FailNTimesProcessor<string>(failCount: int.MaxValue))
                .WriteTo(writer1)
                .WithSkipPolicy(skipPolicy)
                .WithChunkSize(1))
            .Build();

        var result1 = await job1.RunAsync();
        Assert.That(result1.Success, Is.False);
        Assert.That(writer1.Written, Is.Empty);

        // Run 2: processor fails once then succeeds.
        //   Resumes from index 1 (backed up from error at index 2).
        //   index 1 ("b") → fails (call #1) → budget reset, skipped (budget 0/1)
        //   index 2 ("c") → succeeds → written
        //   index 3 ("d") → succeeds → written
        var writer2 = new CollectingWriter<string>();
        var job2 = Job.CreateBuilder(jobName)
            .UseJobStore(ConnectionString, DatabaseProvider.SqlServer)
            .AddStep("step1", step => step
                .ReadFrom(new ListReader<string>(data))
                .ProcessWith(new FailNTimesProcessor<string>(failCount: 1))
                .WriteTo(writer2)
                .WithSkipPolicy(skipPolicy)
                .WithChunkSize(1))
            .Build();

        var result2 = await job2.RunAsync();
        Assert.That(result2.Success, Is.True);
        Assert.That(result2.Steps[0].ErrorsSkipped, Is.EqualTo(1));
        Assert.That(writer2.Written, Is.EqualTo(new[] { "c", "d" }));
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

        // Run 2: same job name → should resume past all data.
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

    #region 7 — Large dataset (50K records from SQL Server DB)

    [Test]
    public async Task Large_dataset_50K_records_chunk500_SqlServer()
    {
        await using var ctx = new TestDbContext(SqlServerOptions);
        var writer = new CollectingWriter<TestRecord>();

        var job = Job.CreateBuilder(UniqueJobName())
            .UseJobStore(ConnectionString, DatabaseProvider.SqlServer)
            .AddStep("read-all", step => step
                .ReadFrom(new DbReader<TestRecord>(ctx, q => q.OrderBy(r => r.Id)))
                .WriteTo(writer)
                .WithChunkSize(500))
            .Build();

        var result = await job.RunAsync();

        Assert.That(result.Success, Is.True);
        Assert.That(result.Steps[0].ItemsRead, Is.EqualTo(50_000));
        Assert.That(result.Steps[0].ItemsProcessed, Is.EqualTo(50_000));
        Assert.That(writer.Written, Has.Count.EqualTo(50_000));
        Assert.That(writer.Written[0].Code, Is.EqualTo("REC-00001"));
        Assert.That(writer.Written[^1].Code, Is.EqualTo("REC-50000"));
    }

    [Test]
    public async Task Large_dataset_exact_chunk_boundary_chunk1000_SqlServer()
    {
        await using var ctx = new TestDbContext(SqlServerOptions);
        var writer = new CollectingWriter<TestRecord>();

        var job = Job.CreateBuilder(UniqueJobName())
            .UseJobStore(ConnectionString, DatabaseProvider.SqlServer)
            .AddStep("read-all", step => step
                .ReadFrom(new DbReader<TestRecord>(ctx, q => q.OrderBy(r => r.Id)))
                .WriteTo(writer)
                .WithChunkSize(1000))
            .Build();

        var result = await job.RunAsync();

        Assert.That(result.Success, Is.True);
        Assert.That(writer.Written, Has.Count.EqualTo(50_000));
    }

    [Test]
    public async Task Large_dataset_uneven_chunk_boundary_chunk499_SqlServer()
    {
        await using var ctx = new TestDbContext(SqlServerOptions);
        var writer = new CollectingWriter<TestRecord>();

        // 50000 / 499 = 100 full chunks + 1 partial chunk of 100 items
        var job = Job.CreateBuilder(UniqueJobName())
            .UseJobStore(ConnectionString, DatabaseProvider.SqlServer)
            .AddStep("read-all", step => step
                .ReadFrom(new DbReader<TestRecord>(ctx, q => q.OrderBy(r => r.Id)))
                .WriteTo(writer)
                .WithChunkSize(499))
            .Build();

        var result = await job.RunAsync();

        Assert.That(result.Success, Is.True);
        Assert.That(writer.Written, Has.Count.EqualTo(50_000));
    }

    [Test]
    public async Task Large_dataset_single_chunk_SqlServer()
    {
        await using var ctx = new TestDbContext(SqlServerOptions);
        var writer = new CollectingWriter<TestRecord>();

        var job = Job.CreateBuilder(UniqueJobName())
            .UseJobStore(ConnectionString, DatabaseProvider.SqlServer)
            .AddStep("read-all", step => step
                .ReadFrom(new DbReader<TestRecord>(ctx, q => q.OrderBy(r => r.Id)))
                .WriteTo(writer)
                .WithChunkSize(50_000))
            .Build();

        var result = await job.RunAsync();

        Assert.That(result.Success, Is.True);
        Assert.That(writer.Written, Has.Count.EqualTo(50_000));
    }

    [Test]
    public async Task Large_dataset_oversized_chunk_SqlServer()
    {
        await using var ctx = new TestDbContext(SqlServerOptions);
        var writer = new CollectingWriter<TestRecord>();

        // Chunk size larger than dataset — still processes all 50K in one chunk
        var job = Job.CreateBuilder(UniqueJobName())
            .UseJobStore(ConnectionString, DatabaseProvider.SqlServer)
            .AddStep("read-all", step => step
                .ReadFrom(new DbReader<TestRecord>(ctx, q => q.OrderBy(r => r.Id)))
                .WriteTo(writer)
                .WithChunkSize(100_000))
            .Build();

        var result = await job.RunAsync();

        Assert.That(result.Success, Is.True);
        Assert.That(writer.Written, Has.Count.EqualTo(50_000));
    }

    [Test]
    public async Task Large_dataset_filtered_by_category_SqlServer()
    {
        await using var ctx = new TestDbContext(SqlServerOptions);
        var writer = new CollectingWriter<TestRecord>();

        // "Alpha" = every 5th record (n % 5 == 0) → 10,000 records
        var job = Job.CreateBuilder(UniqueJobName())
            .UseJobStore(ConnectionString, DatabaseProvider.SqlServer)
            .AddStep("read-alpha", step => step
                .ReadFrom(new DbReader<TestRecord>(ctx, q => q.Where(r => r.Category == "Alpha").OrderBy(r => r.Id)))
                .WriteTo(writer)
                .WithChunkSize(500))
            .Build();

        var result = await job.RunAsync();

        Assert.That(result.Success, Is.True);
        Assert.That(writer.Written, Has.Count.EqualTo(10_000));
        Assert.That(writer.Written.All(r => r.Category == "Alpha"), Is.True);
    }

    [Test]
    public async Task Large_dataset_empty_result_set_SqlServer()
    {
        await using var ctx = new TestDbContext(SqlServerOptions);
        var writer = new CollectingWriter<TestRecord>();

        // Query for a category that doesn't exist → 0 records
        var job = Job.CreateBuilder(UniqueJobName())
            .UseJobStore(ConnectionString, DatabaseProvider.SqlServer)
            .AddStep("read-none", step => step
                .ReadFrom(new DbReader<TestRecord>(ctx, q => q.Where(r => r.Category == "NonExistent").OrderBy(r => r.Id)))
                .WriteTo(writer)
                .WithChunkSize(500))
            .Build();

        var result = await job.RunAsync();

        Assert.That(result.Success, Is.True);
        Assert.That(result.Steps[0].ItemsRead, Is.EqualTo(0));
        Assert.That(writer.Written, Is.Empty);
    }

    #endregion

    #region 8 — Large dataset with transformation

    [Test]
    public async Task Large_dataset_with_processor_SqlServer()
    {
        await using var ctx = new TestDbContext(SqlServerOptions);
        var writer = new CollectingWriter<TestRecordEtl>();

        var job = Job.CreateBuilder(UniqueJobName())
            .UseJobStore(ConnectionString, DatabaseProvider.SqlServer)
            .AddStep("transform", step => step
                .ReadFrom(new DbReader<TestRecord>(ctx, q => q.OrderBy(r => r.Id)))
                .ProcessWith(r => new TestRecordEtl
                {
                    Code = r.Code.ToUpperInvariant(),
                    Value = r.Value * 2,
                    Category = r.Category
                })
                .WriteTo(writer)
                .WithChunkSize(1000))
            .Build();

        var result = await job.RunAsync();

        Assert.That(result.Success, Is.True);
        Assert.That(writer.Written, Has.Count.EqualTo(50_000));
        Assert.That(writer.Written[0].Code, Is.EqualTo("REC-00001"));
        Assert.That(writer.Written[0].Value, Is.EqualTo(1.23m * 2));
    }

    #endregion

    #region 9 — Large dataset restart and skip

    [Test]
    public async Task Large_dataset_restart_from_processor_failure_SqlServer()
    {
        var jobName = UniqueJobName();

        // Run 1: processor fails after 5000 items (5 full chunks of 1000).
        // The 6th chunk fails, job stops.
        var writer1 = new CollectingWriter<TestRecord>();
        await using (var ctx1 = new TestDbContext(SqlServerOptions))
        {
            var job1 = Job.CreateBuilder(jobName)
                .UseJobStore(ConnectionString, DatabaseProvider.SqlServer)
                .AddStep("step1", step => step
                    .ReadFrom(new DbReader<TestRecord>(ctx1, q => q.OrderBy(r => r.Id)))
                    .ProcessWith(new FailAfterNItemsProcessor<TestRecord>(succeedCount: 5000))
                    .WriteTo(writer1)
                    .WithChunkSize(1000))
                .Build();

            var result1 = await job1.RunAsync();
            Assert.That(result1.Success, Is.False);
            Assert.That(writer1.Written, Has.Count.EqualTo(5000));
        }

        // Run 2: fresh processor (no failures). Resumes from chunk 6 onward.
        var writer2 = new CollectingWriter<TestRecord>();
        await using (var ctx2 = new TestDbContext(SqlServerOptions))
        {
            var job2 = Job.CreateBuilder(jobName)
                .UseJobStore(ConnectionString, DatabaseProvider.SqlServer)
                .AddStep("step1", step => step
                    .ReadFrom(new DbReader<TestRecord>(ctx2, q => q.OrderBy(r => r.Id)))
                    .WriteTo(writer2)
                    .WithChunkSize(1000))
                .Build();

            var result2 = await job2.RunAsync();
            Assert.That(result2.Success, Is.True);
            Assert.That(writer2.Written, Has.Count.EqualTo(45_000));
            Assert.That(writer2.Written[0].Code, Is.EqualTo("REC-05001"));
        }
    }

    [Test]
    public async Task Large_dataset_skip_policy_with_db_reader_SqlServer()
    {
        await using var ctx = new TestDbContext(SqlServerOptions);
        var writer = new CollectingWriter<TestRecord>();

        // Processor fails on first 3 items, then succeeds.
        // With chunk size 1 and skip limit 5, first 3 chunks are skipped.
        var skipPolicy = new SkipPolicy([typeof(TimeoutException)], skipLimit: 5);
        var processor = new FailNTimesProcessor<TestRecord>(failCount: 3);

        // Use a small filtered subset (first 10 records) to keep the test fast
        var job = Job.CreateBuilder(UniqueJobName())
            .UseJobStore(ConnectionString, DatabaseProvider.SqlServer)
            .AddStep("step1", step => step
                .ReadFrom(new DbReader<TestRecord>(ctx, q => q.OrderBy(r => r.Id).Take(10)))
                .ProcessWith(processor)
                .WriteTo(writer)
                .WithSkipPolicy(skipPolicy)
                .WithChunkSize(1))
            .Build();

        var result = await job.RunAsync();

        Assert.That(result.Success, Is.True);
        Assert.That(result.Steps[0].ErrorsSkipped, Is.EqualTo(3));
        Assert.That(writer.Written, Has.Count.EqualTo(7));
    }

    #endregion

    #region 10 — Large dataset DB-to-DB ETL within SQL Server

    [Test]
    public async Task Large_dataset_db_to_db_etl_within_SqlServer()
    {
        await TruncateEtlTableAsync();

        await using var readCtx = new TestDbContext(SqlServerOptions);

        var job = Job.CreateBuilder(UniqueJobName())
            .UseJobStore(ConnectionString, DatabaseProvider.SqlServer)
            .AddStep("etl", step => step
                .ReadFrom(new DbReader<TestRecord>(readCtx, q => q.OrderBy(r => r.Id)))
                .ProcessWith(r => new TestRecordEtl
                {
                    Code = r.Code.ToUpperInvariant(),
                    Value = r.Value * 2,
                    Category = r.Category
                })
                .WriteTo(async (IEnumerable<TestRecordEtl> items, CancellationToken ct) =>
                {
                    await using var writeCtx = new TestDbContext(SqlServerOptions);
                    writeCtx.Set<TestRecordEtl>().AddRange(items);
                    await writeCtx.SaveChangesAsync(ct);
                })
                .WithChunkSize(1000))
            .Build();

        var result = await job.RunAsync();
        Assert.That(result.Success, Is.True);
        Assert.That(result.Steps[0].ItemsProcessed, Is.EqualTo(50_000));

        // Verify data in the ETL table
        await using var verifyCtx = new TestDbContext(SqlServerOptions);
        var etlCount = await verifyCtx.TestRecordEtls.CountAsync();
        Assert.That(etlCount, Is.EqualTo(50_000));

        var first = await verifyCtx.TestRecordEtls.OrderBy(r => r.Id).FirstAsync();
        Assert.That(first.Code, Is.EqualTo("REC-00001"));
        Assert.That(first.Value, Is.EqualTo(1.23m * 2));

        await TruncateEtlTableAsync();
    }

    #endregion

    #region 11 — Cancellation

    [Test]
    public async Task Cancellation_stops_large_job_SqlServer()
    {
        await using var ctx = new TestDbContext(SqlServerOptions);
        var cts = new CancellationTokenSource();
        int chunksWritten = 0;

        var job = Job.CreateBuilder(UniqueJobName())
            .UseJobStore(ConnectionString, DatabaseProvider.SqlServer)
            .AddStep("read-all", step => step
                .ReadFrom(new DbReader<TestRecord>(ctx, q => q.OrderBy(r => r.Id)))
                .WriteTo(async (IEnumerable<TestRecord> items, CancellationToken ct) =>
                {
                    if (Interlocked.Increment(ref chunksWritten) >= 3)
                        await cts.CancelAsync();
                })
                .WithChunkSize(1000))
            .Build();

        Assert.ThrowsAsync<OperationCanceledException>(() => job.RunAsync(cts.Token));
        Assert.That(chunksWritten, Is.GreaterThanOrEqualTo(3));
        Assert.That(chunksWritten, Is.LessThan(50));
    }

    #endregion

    #region 12 — Completed DB job produces zero items on re-run

    [Test]
    public async Task Completed_db_job_produces_zero_items_on_rerun_SqlServer()
    {
        var jobName = UniqueJobName();

        var writer1 = new CollectingWriter<TestRecord>();
        await using (var ctx1 = new TestDbContext(SqlServerOptions))
        {
            var job1 = Job.CreateBuilder(jobName)
                .UseJobStore(ConnectionString, DatabaseProvider.SqlServer)
                .AddStep("step1", step => step
                    .ReadFrom(new DbReader<TestRecord>(ctx1, q => q.OrderBy(r => r.Id)))
                    .WriteTo(writer1)
                    .WithChunkSize(5000))
                .Build();

            var result1 = await job1.RunAsync();
            Assert.That(result1.Success, Is.True);
            Assert.That(writer1.Written, Has.Count.EqualTo(50_000));
        }

        // Run 2: same job name → resumes past all data, reads 0 items
        var writer2 = new CollectingWriter<TestRecord>();
        await using (var ctx2 = new TestDbContext(SqlServerOptions))
        {
            var job2 = Job.CreateBuilder(jobName)
                .UseJobStore(ConnectionString, DatabaseProvider.SqlServer)
                .AddStep("step1", step => step
                    .ReadFrom(new DbReader<TestRecord>(ctx2, q => q.OrderBy(r => r.Id)))
                    .WriteTo(writer2)
                    .WithChunkSize(5000))
                .Build();

            var result2 = await job2.RunAsync();
            Assert.That(result2.Success, Is.True);
            Assert.That(result2.Steps[0].ItemsRead, Is.EqualTo(0));
            Assert.That(writer2.Written, Is.Empty);
        }
    }

    #endregion

    #region 13 — Multi-step with DB reader and tasklet

    [Test]
    public async Task Multi_step_db_read_transform_then_tasklet_SqlServer()
    {
        var writer = new CollectingWriter<TestRecordEtl>();
        bool taskletExecuted = false;

        await using var ctx = new TestDbContext(SqlServerOptions);

        var job = Job.CreateBuilder(UniqueJobName())
            .UseJobStore(ConnectionString, DatabaseProvider.SqlServer)
            .AddStep("transform", step => step
                .ReadFrom(new DbReader<TestRecord>(ctx,
                    q => q.Where(r => r.Category == "Beta").OrderBy(r => r.Id)))
                .ProcessWith(r => new TestRecordEtl
                {
                    Code = r.Code.ToUpperInvariant(),
                    Value = r.Value,
                    Category = r.Category
                })
                .WriteTo(writer)
                .WithChunkSize(1000))
            .AddStep("notify", step => step
                .Execute(() =>
                {
                    taskletExecuted = true;
                    return Task.CompletedTask;
                }))
            .Build();

        var result = await job.RunAsync();

        Assert.That(result.Success, Is.True);
        Assert.That(result.Steps, Has.Count.EqualTo(2));
        Assert.That(writer.Written, Has.Count.EqualTo(10_000));
        Assert.That(writer.Written.All(r => r.Category == "Beta"), Is.True);
        Assert.That(taskletExecuted, Is.True);
    }

    #endregion
}
