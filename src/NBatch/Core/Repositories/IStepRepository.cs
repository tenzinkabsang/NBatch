namespace NBatch.Core.Repositories;

public interface IStepRepository
{
    Task<long> InsertStepAsync(string stepName, long stepIndex);
    Task<long> UpdateStepAsync(long stepId, int numberOfItemsProcessed, bool error, bool skipped);
    Task<int> GetExceptionCountAsync(SkipContext skipContext);
    Task SaveExceptionInfoAsync(SkipContext skipContext, int currentCount);
}
