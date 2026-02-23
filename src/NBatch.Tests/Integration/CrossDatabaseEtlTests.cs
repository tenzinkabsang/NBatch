using Microsoft.EntityFrameworkCore;
using NBatch.Core;
using NBatch.Core.Interfaces;
using NBatch.Core.Repositories;
using NBatch.Readers.DbReader;
using NBatch.Tests.Integration.Fixtures;
using NUnit.Framework;

namespace NBatch.Tests.Integration;

/// <summary>
/// Cross-database ETL integration tests exercising pipelines between SQL Server
/// and PostgreSQL via docker-compose.
///
/// Prerequisites:
///   cd src &amp;&amp; docker-compose up -d
///
/// Run these tests with:
///   dotnet test --filter "Category=Docker"
/// </summary>
[TestFixture]
[Category("Docker")]
internal sealed class CrossDatabaseEtlTests
{
    private const string SqlServerConnectionString =
        "Server=localhost,1433;Database=NBatch_IntegrationTests;User Id=sa;Password=@Password1234;TrustServerCertificate=True;";

    private const string PostgreSqlConnectionString =
        "Host=localhost;Port=5432;Database=nbatch_integration;Username=nbatch;Password=Password1234;";

    private static DbContextOptions<TestDbContext> SqlServerOptions
        => TestDbContext.ForSqlServer(SqlServerConnectionString);

    private static DbContextOptions<TestDbContext> PostgreSqlOptions
        => TestDbContext.ForPostgreSql(PostgreSqlConnectionString);

    [SetUp]
    public void BeforeEach()
    {
        EfJobRepository.ResetInitializationCache();
    }

    [TearDown]
    public async Task AfterEach()
    {
        // Clean ETL destination tables in both databases
        await using var sqlCtx = new TestDbContext(SqlServerOptions);
        await sqlCtx.Database.ExecuteSqlRawAsync("DELETE FROM TestRecordEtl");

        await using var pgCtx = new TestDbContext(PostgreSqlOptions);
        await pgCtx.Database.ExecuteSqlRawAsync(@"DELETE FROM ""TestRecordEtl""");
    }

    #region Helpers

    private sealed class CollectingWriter<T> : IWriter<T>
    {
        public List<T> Written { get; } = [];

        public Task WriteAsync(IEnumerable<T> items, CancellationToken cancellationToken = default)
        {
            Written.AddRange(items);
            return Task.CompletedTask;
        }
    }

    private sealed class FailAfterNItemsProcessor<TIn, TOut>(int succeedCount, Func<TIn, TOut> transform) : IProcessor<TIn, TOut>
    {
        private int _processed;

        public Task<TOut> ProcessAsync(TIn input, CancellationToken cancellationToken = default)
        {
            if (_processed >= succeedCount)
                throw new InvalidOperationException($"Simulated failure after {succeedCount} items");
            _processed++;
            return Task.FromResult(transform(input));
        }
    }

    private static string UniqueJobName([System.Runtime.CompilerServices.CallerMemberName] string caller = "")
        => $"{caller}_{Guid.NewGuid():N}";

    private static TestRecordEtl TransformRecord(TestRecord r) => new()
    {
        Code = r.Code.ToUpperInvariant(),
        Value = r.Value * 2,
        Category = r.Category
    };

    #endregion

    #region 1 — SQL Server → PostgreSQL ETL

    [Test]
    public async Task Etl_SqlServer_to_PostgreSql_50K_records()
    {
        await using var readCtx = new TestDbContext(SqlServerOptions);

        // Job store on SQL Server; read from SQL Server, write to PostgreSQL
        var job = Job.CreateBuilder(UniqueJobName())
            .UseJobStore(SqlServerConnectionString, DatabaseProvider.SqlServer)
            .AddStep("etl", step => step
                .ReadFrom(new DbReader<TestRecord>(readCtx, q => q.OrderBy(r => r.Id)))
                .ProcessWith(TransformRecord)
                .WriteTo(async (IEnumerable<TestRecordEtl> items, CancellationToken ct) =>
                {
                    await using var writeCtx = new TestDbContext(PostgreSqlOptions);
                    writeCtx.Set<TestRecordEtl>().AddRange(items);
                    await writeCtx.SaveChangesAsync(ct);
                })
                .WithChunkSize(1000))
            .Build();

        var result = await job.RunAsync();

        Assert.That(result.Success, Is.True);
        Assert.That(result.Steps[0].ItemsProcessed, Is.EqualTo(50_000));

        // Verify in PostgreSQL
        await using var verifyCtx = new TestDbContext(PostgreSqlOptions);
        var count = await verifyCtx.TestRecordEtls.CountAsync();
        Assert.That(count, Is.EqualTo(50_000));

        var first = await verifyCtx.TestRecordEtls.OrderBy(r => r.Id).FirstAsync();
        Assert.That(first.Code, Is.EqualTo("REC-00001"));
        Assert.That(first.Value, Is.EqualTo(1.23m * 2));

        var last = await verifyCtx.TestRecordEtls.OrderByDescending(r => r.Id).FirstAsync();
        Assert.That(last.Code, Is.EqualTo("REC-50000"));
    }

    #endregion

    #region 2 — PostgreSQL → SQL Server ETL

    [Test]
    public async Task Etl_PostgreSql_to_SqlServer_50K_records()
    {
        await using var readCtx = new TestDbContext(PostgreSqlOptions);

        // Job store on PostgreSQL; read from PostgreSQL, write to SQL Server
        var job = Job.CreateBuilder(UniqueJobName())
            .UseJobStore(PostgreSqlConnectionString, DatabaseProvider.PostgreSql)
            .AddStep("etl", step => step
                .ReadFrom(new DbReader<TestRecord>(readCtx, q => q.OrderBy(r => r.Id)))
                .ProcessWith(TransformRecord)
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

        // Verify in SQL Server
        await using var verifyCtx = new TestDbContext(SqlServerOptions);
        var count = await verifyCtx.TestRecordEtls.CountAsync();
        Assert.That(count, Is.EqualTo(50_000));

        var first = await verifyCtx.TestRecordEtls.OrderBy(r => r.Id).FirstAsync();
        Assert.That(first.Code, Is.EqualTo("REC-00001"));
        Assert.That(first.Value, Is.EqualTo(1.23m * 2));
    }

    #endregion

    #region 3 — Cross-DB ETL with filtered subset

    [Test]
    public async Task Etl_SqlServer_to_PostgreSql_filtered_category()
    {
        await using var readCtx = new TestDbContext(SqlServerOptions);

        // Only transfer "Gamma" records (10,000 of 50,000)
        var job = Job.CreateBuilder(UniqueJobName())
            .UseJobStore(SqlServerConnectionString, DatabaseProvider.SqlServer)
            .AddStep("etl-gamma", step => step
                .ReadFrom(new DbReader<TestRecord>(readCtx,
                    q => q.Where(r => r.Category == "Gamma").OrderBy(r => r.Id)))
                .ProcessWith(TransformRecord)
                .WriteTo(async (IEnumerable<TestRecordEtl> items, CancellationToken ct) =>
                {
                    await using var writeCtx = new TestDbContext(PostgreSqlOptions);
                    writeCtx.Set<TestRecordEtl>().AddRange(items);
                    await writeCtx.SaveChangesAsync(ct);
                })
                .WithChunkSize(500))
            .Build();

        var result = await job.RunAsync();

        Assert.That(result.Success, Is.True);
        Assert.That(result.Steps[0].ItemsProcessed, Is.EqualTo(10_000));

        await using var verifyCtx = new TestDbContext(PostgreSqlOptions);
        var count = await verifyCtx.TestRecordEtls.CountAsync();
        Assert.That(count, Is.EqualTo(10_000));
        Assert.That(await verifyCtx.TestRecordEtls.AllAsync(r => r.Category == "Gamma"), Is.True);
    }

    #endregion

    #region 4 — Cross-DB ETL restart from failure

    [Test]
    public async Task Etl_cross_db_restart_from_failure()
    {
        var jobName = UniqueJobName();

        // Run 1: Transfer 5000 items then fail.
        // Processor succeeds for first 5000 items, then throws.
        await using (var readCtx1 = new TestDbContext(SqlServerOptions))
        {
            var job1 = Job.CreateBuilder(jobName)
                .UseJobStore(SqlServerConnectionString, DatabaseProvider.SqlServer)
                .AddStep("etl", step => step
                    .ReadFrom(new DbReader<TestRecord>(readCtx1, q => q.OrderBy(r => r.Id)))
                    .ProcessWith(new FailAfterNItemsProcessor<TestRecord, TestRecordEtl>(
                        succeedCount: 5000, TransformRecord))
                    .WriteTo(async (IEnumerable<TestRecordEtl> items, CancellationToken ct) =>
                    {
                        await using var writeCtx = new TestDbContext(PostgreSqlOptions);
                        writeCtx.Set<TestRecordEtl>().AddRange(items);
                        await writeCtx.SaveChangesAsync(ct);
                    })
                    .WithChunkSize(1000))
                .Build();

            var result1 = await job1.RunAsync();
            Assert.That(result1.Success, Is.False);
        }

        // Verify partial write to PostgreSQL (5 full chunks of 1000)
        await using (var verifyCtx = new TestDbContext(PostgreSqlOptions))
        {
            var partialCount = await verifyCtx.TestRecordEtls.CountAsync();
            Assert.That(partialCount, Is.EqualTo(5000));
        }

        // Run 2: Restart with a processor that always succeeds.
        // Should resume from the failed chunk (index 5000) and transfer remaining 45,000.
        await using (var readCtx2 = new TestDbContext(SqlServerOptions))
        {
            var job2 = Job.CreateBuilder(jobName)
                .UseJobStore(SqlServerConnectionString, DatabaseProvider.SqlServer)
                .AddStep("etl", step => step
                    .ReadFrom(new DbReader<TestRecord>(readCtx2, q => q.OrderBy(r => r.Id)))
                    .ProcessWith(TransformRecord)
                    .WriteTo(async (IEnumerable<TestRecordEtl> items, CancellationToken ct) =>
                    {
                        await using var writeCtx = new TestDbContext(PostgreSqlOptions);
                        writeCtx.Set<TestRecordEtl>().AddRange(items);
                        await writeCtx.SaveChangesAsync(ct);
                    })
                    .WithChunkSize(1000))
                .Build();

            var result2 = await job2.RunAsync();
            Assert.That(result2.Success, Is.True);
        }

        // Verify all 50,000 records in PostgreSQL
        await using (var verifyCtx = new TestDbContext(PostgreSqlOptions))
        {
            var totalCount = await verifyCtx.TestRecordEtls.CountAsync();
            Assert.That(totalCount, Is.EqualTo(50_000));
        }
    }

    #endregion

    #region 5 — Multi-step cross-DB pipeline

    [Test]
    public async Task Multi_step_cross_db_pipeline()
    {
        var alphaWriter = new CollectingWriter<TestRecordEtl>();
        bool notifyExecuted = false;

        await using var sqlCtx = new TestDbContext(SqlServerOptions);
        await using var pgCtx = new TestDbContext(PostgreSqlOptions);

        // Step 1: Read "Alpha" from SQL Server → collect in memory
        // Step 2: Read "Beta" from PostgreSQL → write to SQL Server ETL table
        // Step 3: Tasklet notification
        var job = Job.CreateBuilder(UniqueJobName())
            .UseJobStore(SqlServerConnectionString, DatabaseProvider.SqlServer)
            .AddStep("alpha-from-sql", step => step
                .ReadFrom(new DbReader<TestRecord>(sqlCtx,
                    q => q.Where(r => r.Category == "Alpha").OrderBy(r => r.Id)))
                .ProcessWith(TransformRecord)
                .WriteTo(alphaWriter)
                .WithChunkSize(1000))
            .AddStep("beta-from-pg", step => step
                .ReadFrom(new DbReader<TestRecord>(pgCtx,
                    q => q.Where(r => r.Category == "Beta").OrderBy(r => r.Id)))
                .ProcessWith(TransformRecord)
                .WriteTo(async (IEnumerable<TestRecordEtl> items, CancellationToken ct) =>
                {
                    await using var writeCtx = new TestDbContext(SqlServerOptions);
                    writeCtx.Set<TestRecordEtl>().AddRange(items);
                    await writeCtx.SaveChangesAsync(ct);
                })
                .WithChunkSize(1000))
            .AddStep("notify", step => step
                .Execute(() =>
                {
                    notifyExecuted = true;
                    return Task.CompletedTask;
                }))
            .Build();

        var result = await job.RunAsync();

        Assert.That(result.Success, Is.True);
        Assert.That(result.Steps, Has.Count.EqualTo(3));

        // Step 1: 10,000 Alpha records from SQL Server
        Assert.That(alphaWriter.Written, Has.Count.EqualTo(10_000));
        Assert.That(alphaWriter.Written.All(r => r.Category == "Alpha"), Is.True);

        // Step 2: 10,000 Beta records from PostgreSQL → SQL Server ETL table
        await using var verifyCtx = new TestDbContext(SqlServerOptions);
        var betaCount = await verifyCtx.TestRecordEtls.CountAsync();
        Assert.That(betaCount, Is.EqualTo(10_000));
        Assert.That(await verifyCtx.TestRecordEtls.AllAsync(r => r.Category == "Beta"), Is.True);

        // Step 3: tasklet ran
        Assert.That(notifyExecuted, Is.True);
    }

    #endregion

    #region 6 — Bidirectional ETL (swap data between databases)

    [Test]
    public async Task Bidirectional_etl_swaps_categories_between_databases()
    {
        // Transfer "Delta" from SQL Server → PostgreSQL ETL table
        // Transfer "Epsilon" from PostgreSQL → SQL Server ETL table
        await using var sqlReadCtx = new TestDbContext(SqlServerOptions);
        await using var pgReadCtx = new TestDbContext(PostgreSqlOptions);

        var job = Job.CreateBuilder(UniqueJobName())
            .UseJobStore(SqlServerConnectionString, DatabaseProvider.SqlServer)
            .AddStep("delta-to-pg", step => step
                .ReadFrom(new DbReader<TestRecord>(sqlReadCtx,
                    q => q.Where(r => r.Category == "Delta").OrderBy(r => r.Id)))
                .ProcessWith(TransformRecord)
                .WriteTo(async (IEnumerable<TestRecordEtl> items, CancellationToken ct) =>
                {
                    await using var writeCtx = new TestDbContext(PostgreSqlOptions);
                    writeCtx.Set<TestRecordEtl>().AddRange(items);
                    await writeCtx.SaveChangesAsync(ct);
                })
                .WithChunkSize(1000))
            .AddStep("epsilon-to-sql", step => step
                .ReadFrom(new DbReader<TestRecord>(pgReadCtx,
                    q => q.Where(r => r.Category == "Epsilon").OrderBy(r => r.Id)))
                .ProcessWith(TransformRecord)
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
        Assert.That(result.Steps, Has.Count.EqualTo(2));

        // Verify Delta in PostgreSQL
        await using var pgVerify = new TestDbContext(PostgreSqlOptions);
        var deltaCount = await pgVerify.TestRecordEtls.CountAsync();
        Assert.That(deltaCount, Is.EqualTo(10_000));
        Assert.That(await pgVerify.TestRecordEtls.AllAsync(r => r.Category == "Delta"), Is.True);

        // Verify Epsilon in SQL Server
        await using var sqlVerify = new TestDbContext(SqlServerOptions);
        var epsilonCount = await sqlVerify.TestRecordEtls.CountAsync();
        Assert.That(epsilonCount, Is.EqualTo(10_000));
        Assert.That(await sqlVerify.TestRecordEtls.AllAsync(r => r.Category == "Epsilon"), Is.True);
    }

    #endregion

    #region 7 — Cross-DB ETL with job store on the opposite provider

    [Test]
    public async Task Etl_SqlServer_to_PostgreSql_with_job_store_on_PostgreSql()
    {
        await using var readCtx = new TestDbContext(SqlServerOptions);

        // Job store on PostgreSQL, data flows SQL Server → PostgreSQL
        var job = Job.CreateBuilder(UniqueJobName())
            .UseJobStore(PostgreSqlConnectionString, DatabaseProvider.PostgreSql)
            .AddStep("etl", step => step
                .ReadFrom(new DbReader<TestRecord>(readCtx,
                    q => q.Where(r => r.Category == "Alpha").OrderBy(r => r.Id)))
                .ProcessWith(TransformRecord)
                .WriteTo(async (IEnumerable<TestRecordEtl> items, CancellationToken ct) =>
                {
                    await using var writeCtx = new TestDbContext(PostgreSqlOptions);
                    writeCtx.Set<TestRecordEtl>().AddRange(items);
                    await writeCtx.SaveChangesAsync(ct);
                })
                .WithChunkSize(500))
            .Build();

        var result = await job.RunAsync();

        Assert.That(result.Success, Is.True);
        Assert.That(result.Steps[0].ItemsProcessed, Is.EqualTo(10_000));

        await using var verifyCtx = new TestDbContext(PostgreSqlOptions);
        var count = await verifyCtx.TestRecordEtls.CountAsync();
        Assert.That(count, Is.EqualTo(10_000));
    }

    #endregion
}
