namespace NBatch.Core.Repositories
{
    public interface IStepRepository
    {
        void SaveStepContext(StepContext stepContext);
        int GetExceptionCount(SkipContext skipContext);
        void SaveExceptionInfo(SkipContext skipContext, int currentCount);
    }
}