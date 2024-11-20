using System.Threading.Tasks;

namespace NBatch.Main.Core.Repositories;

public interface IStepRepository
{
    Task<long> InsertStepAsync(string stepName, long stepIndex);
    Task<long> UpdateStepAsync(long stepId, int numberOfItemsProcessed, bool error, bool skipped);
    Task<int> GetExceptionCountAsync(SkipContext skipContext);
    Task SaveExceptionInfo(SkipContext skipContext, int currentCount);
}