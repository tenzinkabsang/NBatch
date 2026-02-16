namespace NBatch.Core.Repositories;

internal interface IStepRepository
{
    Task<long> InsertStepAsync(string stepName, long stepIndex, CancellationToken cancellationToken = default);
    Task UpdateStepAsync(long stepId, int numberOfItemsProcessed, bool error, bool skipped, CancellationToken cancellationToken = default);
    Task<int> GetExceptionCountAsync(SkipContext skipContext, CancellationToken cancellationToken = default);
    Task SaveExceptionInfoAsync(SkipContext skipContext, CancellationToken cancellationToken = default);
    Task<StepContext> GetStartIndexAsync(string stepName, CancellationToken cancellationToken = default);
}
