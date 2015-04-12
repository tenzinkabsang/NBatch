namespace NBatch.Main.Core.Repositories
{
    public interface IStepRepository
    {
        long InsertStep(string stepName, long stepIndex);
        long UpdateStep(long stepId, int numberOfItemsProcessed, bool error, bool skipped);
        int GetExceptionCount(SkipContext skipContext);
        void SaveExceptionInfo(SkipContext skipContext, int currentCount);
    }
}