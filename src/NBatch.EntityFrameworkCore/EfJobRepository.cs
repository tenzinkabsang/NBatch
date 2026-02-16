using System.Collections.Concurrent;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using NBatch.Core.Repositories.Entities;

namespace NBatch.Core.Repositories;

internal sealed class EfJobRepository : IJobRepository
{
    private readonly string _jobName;
    private readonly string _connectionString;
    private readonly DbContextOptions<NBatchDbContext> _options;
    private long _executionId;

    private static readonly ConcurrentDictionary<string, bool> _initializedDatabases = new();
    private static readonly SemaphoreSlim _initLock = new(1, 1);

    /// <summary>
    /// Clears the database initialization cache. Intended for test scenarios
    /// where each test uses a fresh database.
    /// </summary>
    internal static void ResetInitializationCache() => _initializedDatabases.Clear();

    public EfJobRepository(string jobName, string connectionString, DatabaseProvider provider)
    {
        _jobName = jobName;
        _connectionString = connectionString;
        _options = NBatchDbContext.CreateOptions(connectionString, provider);
    }

    private NBatchDbContext CreateContext() => new(_options);

    private async Task EnsureDatabaseCreatedAsync(CancellationToken cancellationToken)
    {
        if (_initializedDatabases.ContainsKey(_connectionString)) return;

        await _initLock.WaitAsync(cancellationToken);
        try
        {
            if (_initializedDatabases.ContainsKey(_connectionString)) return;
            await using var ctx = CreateContext();
            var creator = (RelationalDatabaseCreator)ctx.Database.GetService<IDatabaseCreator>();
            if (!await creator.ExistsAsync(cancellationToken))
            {
                await ctx.Database.EnsureCreatedAsync(cancellationToken);
            }
            else if (!await TablesExistAsync(ctx, cancellationToken))
            {
                await creator.CreateTablesAsync(cancellationToken);
            }
            _initializedDatabases.TryAdd(_connectionString, true);
        }
        finally
        {
            _initLock.Release();
        }
    }

    private static async Task<bool> TablesExistAsync(NBatchDbContext ctx, CancellationToken cancellationToken)
    {
        try
        {
            await ctx.BatchJobs.AnyAsync(cancellationToken);
            return true;
        }
        catch (DbException)
        {
            return false;
        }
    }

    public async Task<long> CreateJobRecordAsync(ICollection<string> stepNames, CancellationToken cancellationToken = default)
    {
        await EnsureDatabaseCreatedAsync(cancellationToken);

        await using var ctx = CreateContext();
        var job = await ctx.BatchJobs.FindAsync(new object[] { _jobName }, cancellationToken);

        if (job != null)
        {
            job.LastRun = DateTime.UtcNow;
        }
        else
        {
            ctx.BatchJobs.Add(new JobEntity { JobName = _jobName, LastRun = DateTime.UtcNow });

            foreach (var stepName in stepNames)
            {
                ctx.BatchSteps.Add(new StepEntity
                {
                    StepName = stepName,
                    JobName = _jobName,
                    StepIndex = 0,
                    NumberOfItemsProcessed = 0
                });
            }
        }

        await ctx.SaveChangesAsync(cancellationToken);

        // Use the job's LastRun ticks as a stable execution identifier for this run.
        // Exception counts are scoped to this value so skip budgets reset per execution.
        _executionId = (job?.LastRun ?? DateTime.UtcNow).Ticks;
        return _executionId;
    }

    public async Task<long> InsertStepAsync(string stepName, long stepIndex, CancellationToken cancellationToken = default)
    {
        await using var ctx = CreateContext();
        var step = new StepEntity
        {
            StepName = stepName,
            JobName = _jobName,
            StepIndex = stepIndex,
            NumberOfItemsProcessed = 0
        };
        ctx.BatchSteps.Add(step);
        await ctx.SaveChangesAsync(cancellationToken);
        return step.Id;
    }

    public async Task UpdateStepAsync(long stepId, int numberOfItemsProcessed, bool error, bool skipped, CancellationToken cancellationToken = default)
    {
        await using var ctx = CreateContext();
        var step = await ctx.BatchSteps.FindAsync([stepId], cancellationToken);
        if (step != null)
        {
            step.NumberOfItemsProcessed = numberOfItemsProcessed;
            step.Error = error;
            step.Skipped = skipped;
            await ctx.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<int> GetExceptionCountAsync(SkipContext skipContext, CancellationToken cancellationToken = default)
    {
        await using var ctx = CreateContext();
        return await ctx.BatchStepExceptions
            .CountAsync(e => e.StepName == skipContext.StepName
                          && e.JobName == _jobName
                          && e.ExecutionId == _executionId, cancellationToken);
    }

    public async Task<StepContext> GetStartIndexAsync(string stepName, CancellationToken cancellationToken = default)
    {
        await using var ctx = CreateContext();
        var step = await ctx.BatchSteps
            .Where(s => s.StepName == stepName && s.JobName == _jobName)
            .OrderByDescending(s => s.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return new StepContext
        {
            StepName = stepName,
            JobName = _jobName,
            StepIndex = step?.StepIndex ?? 0,
            NumberOfItemsProcessed = step?.NumberOfItemsProcessed ?? 0,
            Error = step?.Error ?? false
        };
    }

    public async Task SaveExceptionInfoAsync(SkipContext skipContext, CancellationToken cancellationToken = default)
    {
        await using var ctx = CreateContext();
        ctx.BatchStepExceptions.Add(new StepExceptionEntity
        {
            StepIndex = skipContext.StepIndex,
            StepName = skipContext.StepName,
            JobName = _jobName,
            ExecutionId = _executionId,
            ExceptionMsg = skipContext.ExceptionMessage,
            ExceptionDetails = skipContext.ExceptionDetail
        });
        await ctx.SaveChangesAsync(cancellationToken);
    }
}
