using Microsoft.EntityFrameworkCore;
using NBatch.Core;
using NBatch.Core.Interfaces;
using NBatch.Core.Repositories;
using NBatch.Readers.DbReader;
using NBatch.Tests.Integration.Fixtures;
using NUnit.Framework;

namespace NBatch.Tests.Integration;

/// <summary>
/// Integration tests that run against a real PostgreSQL instance via docker-compose.
///
/// Prerequisites:
///   cd src &amp;&amp; docker-compose up -d
///
/// Run these tests with:
///   dotnet test --filter "Category=Docker"
/// </summary>
[TestFixture]
[Category("Docker")]
internal sealed class PostgreSqlIntegrationTests
{
    private const string ConnectionString =
        "Host=localhost;Port=5432;Database=nbatch_integration;Username=nbatch;Password=Password1234;";

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

    private static string UniqueJobName([System.Runtime.CompilerServices.CallerMemberName] string caller = "")
        => $"{caller}_{Guid.NewGuid():N}";

    private static DbContextOptions<TestDbContext> PgOptions
        => TestDbContext.ForPostgreSql(ConnectionString);

    private async Task TruncateEtlTableAsync()
    {
        await using var ctx = new TestDbContext(PgOptions);
        await ctx.Database.ExecuteSqlRawAsync(@"DELETE FROM ""TestRecordEtl""");
    }

    #endregion

    #region 1 — Basic chunk processing with PostgreSQL

    [Test]
    public async Task Job_processes_all_chunks_with_PostgreSql()
    {
        var data = new[] { "a", "b", "c", "d", "e" };
        var writer = new CollectingWriter<string>();

        var job = Job.CreateBuilder(UniqueJobName())
            .UseJobStore(ConnectionString, DatabaseProvider.PostgreSql)
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

    #region 2 — Restart from failure with PostgreSQL

    [Test]
    public async Task Job_restart_resumes_from_failed_chunk_with_PostgreSql()
    {
        var data = new[] { "a", "b", "c", "d" };
        var jobName = UniqueJobName();

        var failReader = new FailOnceAtIndexReader<string>(data, failAtIndex: 2);
        var writer = new CollectingWriter<string>();

        var job1 = Job.CreateBuilder(jobName)
            .UseJobStore(ConnectionString, DatabaseProvider.PostgreSql)
            .AddStep("step1", step => step
                .ReadFrom(failReader)
                .WriteTo(writer)
                .WithChunkSize(2))
            .Build();

        var result1 = await job1.RunAsync();
        Assert.That(result1.Success, Is.False);
        Assert.That(writer.Written, Is.EqualTo(new[] { "a", "b" }));

        var job2 = Job.CreateBuilder(jobName)
            .UseJobStore(ConnectionString, DatabaseProvider.PostgreSql)
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

    #region 3 — Skip policy with PostgreSQL

    [Test]
    public async Task Skip_policy_works_with_PostgreSql()
    {
        var data = new[] { "a", "b" };
        var processor = new FailNTimesProcessor<string>(failCount: 1);
        var skipPolicy = new SkipPolicy([typeof(TimeoutException)], skipLimit: 2);
        var writer = new CollectingWriter<string>();

        var job = Job.CreateBuilder(UniqueJobName())
            .UseJobStore(ConnectionString, DatabaseProvider.PostgreSql)
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

    #endregion

    #region 4 — Large dataset (50K records from PostgreSQL DB)

    [Test]
    public async Task Large_dataset_50K_records_chunk500_PostgreSql()
    {
        await using var ctx = new TestDbContext(PgOptions);
        var writer = new CollectingWriter<TestRecord>();

        var job = Job.CreateBuilder(UniqueJobName())
            .UseJobStore(ConnectionString, DatabaseProvider.PostgreSql)
            .AddStep("read-all", step => step
                .ReadFrom(new DbReader<TestRecord>(ctx, q => q.OrderBy(r => r.Id)))
                .WriteTo(writer)
                .WithChunkSize(500))
            .Build();

        var result = await job.RunAsync();

        Assert.That(result.Success, Is.True);
        Assert.That(result.Steps[0].ItemsRead, Is.EqualTo(50_000));
        Assert.That(writer.Written, Has.Count.EqualTo(50_000));
        Assert.That(writer.Written[0].Code, Is.EqualTo("REC-00001"));
        Assert.That(writer.Written[^1].Code, Is.EqualTo("REC-50000"));
    }

    [Test]
    public async Task Large_dataset_exact_chunk_boundary_chunk1000_PostgreSql()
    {
        await using var ctx = new TestDbContext(PgOptions);
        var writer = new CollectingWriter<TestRecord>();

        var job = Job.CreateBuilder(UniqueJobName())
            .UseJobStore(ConnectionString, DatabaseProvider.PostgreSql)
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
    public async Task Large_dataset_uneven_chunk_boundary_chunk499_PostgreSql()
    {
        await using var ctx = new TestDbContext(PgOptions);
        var writer = new CollectingWriter<TestRecord>();

        var job = Job.CreateBuilder(UniqueJobName())
            .UseJobStore(ConnectionString, DatabaseProvider.PostgreSql)
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
    public async Task Large_dataset_single_chunk_PostgreSql()
    {
        await using var ctx = new TestDbContext(PgOptions);
        var writer = new CollectingWriter<TestRecord>();

        var job = Job.CreateBuilder(UniqueJobName())
            .UseJobStore(ConnectionString, DatabaseProvider.PostgreSql)
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
    public async Task Large_dataset_oversized_chunk_PostgreSql()
    {
        await using var ctx = new TestDbContext(PgOptions);
        var writer = new CollectingWriter<TestRecord>();

        var job = Job.CreateBuilder(UniqueJobName())
            .UseJobStore(ConnectionString, DatabaseProvider.PostgreSql)
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
    public async Task Large_dataset_filtered_by_category_PostgreSql()
    {
        await using var ctx = new TestDbContext(PgOptions);
        var writer = new CollectingWriter<TestRecord>();

        var job = Job.CreateBuilder(UniqueJobName())
            .UseJobStore(ConnectionString, DatabaseProvider.PostgreSql)
            .AddStep("read-alpha", step => step
                .ReadFrom(new DbReader<TestRecord>(ctx,
                    q => q.Where(r => r.Category == "Alpha").OrderBy(r => r.Id)))
                .WriteTo(writer)
                .WithChunkSize(500))
            .Build();

        var result = await job.RunAsync();

        Assert.That(result.Success, Is.True);
        Assert.That(writer.Written, Has.Count.EqualTo(10_000));
        Assert.That(writer.Written.All(r => r.Category == "Alpha"), Is.True);
    }

    [Test]
    public async Task Large_dataset_empty_result_set_PostgreSql()
    {
        await using var ctx = new TestDbContext(PgOptions);
        var writer = new CollectingWriter<TestRecord>();

        var job = Job.CreateBuilder(UniqueJobName())
            .UseJobStore(ConnectionString, DatabaseProvider.PostgreSql)
            .AddStep("read-none", step => step
                .ReadFrom(new DbReader<TestRecord>(ctx,
                    q => q.Where(r => r.Category == "NonExistent").OrderBy(r => r.Id)))
                .WriteTo(writer)
                .WithChunkSize(500))
            .Build();

        var result = await job.RunAsync();

        Assert.That(result.Success, Is.True);
        Assert.That(result.Steps[0].ItemsRead, Is.EqualTo(0));
        Assert.That(writer.Written, Is.Empty);
    }

    #endregion

    #region 5 — Large dataset with transformation (PostgreSQL)

    [Test]
    public async Task Large_dataset_with_processor_PostgreSql()
    {
        await using var ctx = new TestDbContext(PgOptions);
        var writer = new CollectingWriter<TestRecordEtl>();

        var job = Job.CreateBuilder(UniqueJobName())
            .UseJobStore(ConnectionString, DatabaseProvider.PostgreSql)
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

    #region 6 — Large dataset restart (PostgreSQL)

    [Test]
    public async Task Large_dataset_restart_from_processor_failure_PostgreSql()
    {
        var jobName = UniqueJobName();

        var writer1 = new CollectingWriter<TestRecord>();
        await using (var ctx1 = new TestDbContext(PgOptions))
        {
            var job1 = Job.CreateBuilder(jobName)
                .UseJobStore(ConnectionString, DatabaseProvider.PostgreSql)
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

        var writer2 = new CollectingWriter<TestRecord>();
        await using (var ctx2 = new TestDbContext(PgOptions))
        {
            var job2 = Job.CreateBuilder(jobName)
                .UseJobStore(ConnectionString, DatabaseProvider.PostgreSql)
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

    #endregion

    #region 7 — Large dataset DB-to-DB ETL within PostgreSQL

    [Test]
    public async Task Large_dataset_db_to_db_etl_within_PostgreSql()
    {
        await TruncateEtlTableAsync();

        await using var readCtx = new TestDbContext(PgOptions);

        var job = Job.CreateBuilder(UniqueJobName())
            .UseJobStore(ConnectionString, DatabaseProvider.PostgreSql)
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
                    await using var writeCtx = new TestDbContext(PgOptions);
                    writeCtx.Set<TestRecordEtl>().AddRange(items);
                    await writeCtx.SaveChangesAsync(ct);
                })
                .WithChunkSize(1000))
            .Build();

        var result = await job.RunAsync();
        Assert.That(result.Success, Is.True);
        Assert.That(result.Steps[0].ItemsProcessed, Is.EqualTo(50_000));

        await using var verifyCtx = new TestDbContext(PgOptions);
        var etlCount = await verifyCtx.TestRecordEtls.CountAsync();
        Assert.That(etlCount, Is.EqualTo(50_000));

        var first = await verifyCtx.TestRecordEtls.OrderBy(r => r.Id).FirstAsync();
        Assert.That(first.Code, Is.EqualTo("REC-00001"));
        Assert.That(first.Value, Is.EqualTo(1.23m * 2));

        await TruncateEtlTableAsync();
    }

    #endregion

    #region 8 — Tasklet steps with PostgreSQL

    [Test]
    public async Task Tasklet_step_success_recorded_with_PostgreSql()
    {
        bool executed = false;

        var job = Job.CreateBuilder(UniqueJobName())
            .UseJobStore(ConnectionString, DatabaseProvider.PostgreSql)
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
    public async Task Tasklet_step_failure_recorded_with_PostgreSql()
    {
        var job = Job.CreateBuilder(UniqueJobName())
            .UseJobStore(ConnectionString, DatabaseProvider.PostgreSql)
            .AddStep("tasklet", step => step
                .Execute(() => throw new InvalidOperationException("boom")))
            .Build();

        var result = await job.RunAsync();

        Assert.That(result.Success, Is.False);
    }

    #endregion

    #region 9 — Cancellation with PostgreSQL

    [Test]
    public async Task Cancellation_stops_large_job_PostgreSql()
    {
        await using var ctx = new TestDbContext(PgOptions);
        var cts = new CancellationTokenSource();
        int chunksWritten = 0;

        var job = Job.CreateBuilder(UniqueJobName())
            .UseJobStore(ConnectionString, DatabaseProvider.PostgreSql)
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

    #region 10 — Completed DB job produces zero items on re-run (PostgreSQL)

    [Test]
    public async Task Completed_db_job_produces_zero_items_on_rerun_PostgreSql()
    {
        var jobName = UniqueJobName();

        var writer1 = new CollectingWriter<TestRecord>();
        await using (var ctx1 = new TestDbContext(PgOptions))
        {
            var job1 = Job.CreateBuilder(jobName)
                .UseJobStore(ConnectionString, DatabaseProvider.PostgreSql)
                .AddStep("step1", step => step
                    .ReadFrom(new DbReader<TestRecord>(ctx1, q => q.OrderBy(r => r.Id)))
                    .WriteTo(writer1)
                    .WithChunkSize(5000))
                .Build();

            var result1 = await job1.RunAsync();
            Assert.That(result1.Success, Is.True);
            Assert.That(writer1.Written, Has.Count.EqualTo(50_000));
        }

        var writer2 = new CollectingWriter<TestRecord>();
        await using (var ctx2 = new TestDbContext(PgOptions))
        {
            var job2 = Job.CreateBuilder(jobName)
                .UseJobStore(ConnectionString, DatabaseProvider.PostgreSql)
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
}
