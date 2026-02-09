namespace NBatch.Core.Repositories;

internal sealed class InMemoryJobRepository(string jobName) : IJobRepository
{
    private long _nextId;
    private readonly Dictionary<long, StepEntry> _steps = [];
    private readonly List<ExceptionEntry> _exceptions = [];

    public Task CreateJobRecordAsync(ICollection<string> stepNames)
    {
        foreach (var stepName in stepNames)
        {
            long id = ++_nextId;
            _steps[id] = new StepEntry(stepName, StepIndex: 0, NumberOfItemsProcessed: 0);
        }
        return Task.CompletedTask;
    }

    public Task<StepContext> GetStartIndexAsync(string stepName)
    {
        var latest = _steps
            .Where(s => s.Value.StepName == stepName)
            .OrderByDescending(s => s.Key)
            .Select(s => s.Value)
            .FirstOrDefault();

        return Task.FromResult(new StepContext
        {
            StepName = stepName,
            JobName = jobName,
            StepIndex = latest?.StepIndex ?? 0,
            NumberOfItemsProcessed = latest?.NumberOfItemsProcessed ?? 0
        });
    }

    public Task<long> InsertStepAsync(string stepName, long stepIndex)
    {
        long id = ++_nextId;
        _steps[id] = new StepEntry(stepName, stepIndex, NumberOfItemsProcessed: 0);
        return Task.FromResult(id);
    }

    public Task UpdateStepAsync(long stepId, int numberOfItemsProcessed, bool error, bool skipped)
    {
        if (_steps.TryGetValue(stepId, out var entry))
            _steps[stepId] = entry with { NumberOfItemsProcessed = numberOfItemsProcessed };

        return Task.CompletedTask;
    }

    public Task<int> GetExceptionCountAsync(SkipContext skipContext)
    {
        int count = _exceptions.Count(e => e.StepName == skipContext.StepName);
        return Task.FromResult(count);
    }

    public Task SaveExceptionInfoAsync(SkipContext skipContext, int currentCount)
    {
        _exceptions.Add(new ExceptionEntry(skipContext.StepName, skipContext.StepIndex, skipContext.ExceptionMessage, skipContext.ExceptionDetail));
        return Task.CompletedTask;
    }

    private sealed record StepEntry(string StepName, long StepIndex, int NumberOfItemsProcessed);
    private sealed record ExceptionEntry(string StepName, long StepIndex, string Message, string Details);
}
