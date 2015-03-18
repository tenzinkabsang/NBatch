namespace NBatch.Core.Repositories
{
    public interface IStepRepository
    {
        void SaveIndex(int index);
        int GetExceptionCount();
        void IncrementExceptionCount();
        void SaveExceptionDetails(SkipContext skipContext);
    }
}