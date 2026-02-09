using Microsoft.EntityFrameworkCore;
using NBatch.Core.Repositories.Entities;

namespace NBatch.Core.Repositories;

internal sealed class EfJobRepository : IJobRepository
{
    private readonly string _jobName;
    private readonly DbContextOptions<NBatchDbContext> _options;
    private bool _databaseInitialized;

    public EfJobRepository(string jobName, string connectionString, DatabaseProvider provider)
    {
        _jobName = jobName;
        _options = NBatchDbContext.CreateOptions(connectionString, provider);
    }

    private NBatchDbContext CreateContext() => new(_options);

    private async Task EnsureDatabaseCreatedAsync()
    {
        if (_databaseInitialized) return;
        await using var ctx = CreateContext();
        await ctx.Database.EnsureCreatedAsync();
        _databaseInitialized = true;
    }

    public async Task CreateJobRecordAsync(ICollection<string> stepNames)
    {
        await EnsureDatabaseCreatedAsync();

        await using var ctx = CreateContext();
        var job = await ctx.BatchJobs.FindAsync(_jobName);

        if (job != null)
        {
            job.LastRun = DateTime.UtcNow;
        }
        else
        {
            ctx.BatchJobs.Add(new BatchJobEntity { JobName = _jobName, LastRun = DateTime.UtcNow });

            foreach (var stepName in stepNames)
            {
                ctx.BatchSteps.Add(new BatchStepEntity
                {
                    StepName = stepName,
                    JobName = _jobName,
                    StepIndex = 0,
                    NumberOfItemsProcessed = 0
                });
            }
        }

        await ctx.SaveChangesAsync();
    }

    public async Task<long> InsertStepAsync(string stepName, long stepIndex)
    {
        await using var ctx = CreateContext();
        var step = new BatchStepEntity
        {
            StepName = stepName,
            JobName = _jobName,
            StepIndex = stepIndex,
            NumberOfItemsProcessed = 0
        };
        ctx.BatchSteps.Add(step);
        await ctx.SaveChangesAsync();
        return step.Id;
    }

    public async Task UpdateStepAsync(long stepId, int numberOfItemsProcessed, bool error, bool skipped)
    {
        await using var ctx = CreateContext();
        var step = await ctx.BatchSteps.FindAsync(stepId);
        if (step != null)
        {
            step.NumberOfItemsProcessed = numberOfItemsProcessed;
            step.Error = error;
            step.Skipped = skipped;
            await ctx.SaveChangesAsync();
        }
    }

    public async Task<int> GetExceptionCountAsync(SkipContext skipContext)
    {
        await using var ctx = CreateContext();
        return await ctx.BatchStepExceptions
            .CountAsync(e => e.StepName == skipContext.StepName && e.JobName == _jobName);
    }

    public async Task<StepContext> GetStartIndexAsync(string stepName)
    {
        await using var ctx = CreateContext();
        var step = await ctx.BatchSteps
            .Where(s => s.StepName == stepName && s.JobName == _jobName)
            .OrderByDescending(s => s.Id)
            .FirstOrDefaultAsync();

        return new StepContext
        {
            StepName = stepName,
            JobName = _jobName,
            StepIndex = step?.StepIndex ?? 0,
            NumberOfItemsProcessed = step?.NumberOfItemsProcessed ?? 0
        };
    }

    public async Task SaveExceptionInfoAsync(SkipContext skipContext, int currentCount)
    {
        await using var ctx = CreateContext();
        ctx.BatchStepExceptions.Add(new BatchStepExceptionEntity
        {
            StepIndex = skipContext.StepIndex,
            StepName = skipContext.StepName,
            JobName = _jobName,
            ExceptionMsg = skipContext.ExceptionMessage,
            ExceptionDetails = skipContext.ExceptionDetail
        });
        await ctx.SaveChangesAsync();
    }
}
