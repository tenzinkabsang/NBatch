using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NBatch.Main.Core.Repositories;

sealed class InMemoryJobRepository : IJobRepository
{
    private readonly IList<long> _dbIndexes = [0];
    private int _exceptionCount = 0;


    public Task<StepContext> GetStartIndexAsync(string stepName)
    {
        throw new NotImplementedException();
    }

    public Task CreateJobRecord(ICollection<string> stepNames)
    {
        return Task.CompletedTask;
    }

    public Task<long> InsertStepAsync(string stepName, long stepIndex)
    {
        return Task.FromResult(0L);
    }

    public Task<long> UpdateStepAsync(long stepId, int numberOfItemsProcessed, bool error, bool skipped)
    {
        return Task.FromResult(0L);
    }
    
    public void SaveStepContext(StepContext stepContext)
    {
        _dbIndexes.Add(stepContext.StepIndex);
    }

    public Task<int> GetExceptionCountAsync(SkipContext context)
    {
        return Task.FromResult(_exceptionCount);
    }

    public Task SaveExceptionInfo(SkipContext skipContext, int currentCount)
    {
        ++_exceptionCount;
        Console.WriteLine("Skippable exception on line: {0} - {1}", skipContext.StepIndex, skipContext.Exception.Message);
        return Task.CompletedTask;
    }
}