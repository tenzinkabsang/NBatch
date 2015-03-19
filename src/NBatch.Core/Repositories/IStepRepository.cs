namespace NBatch.Core.Repositories
{
    public interface IStepRepository
    {
        void SaveIndex(string stepName, int index);
        int GetExceptionCount(SkipContext skipContext);
        void IncrementExceptionCount(SkipContext skipContext, int currentCount);
    }
}