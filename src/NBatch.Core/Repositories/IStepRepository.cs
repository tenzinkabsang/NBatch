namespace NBatch.Core.Repositories
{
    public interface IStepRepository
    {
        void SaveIndex(string stepName, long index);
        int GetExceptionCount(SkipContext skipContext);
        void SaveExceptionInfo(SkipContext skipContext, int currentCount);
    }
}