
namespace NBatch.Core.Repositories
{
    public interface IJobRepository
    {
        int GetStartIndex();
        void SaveIndex(int index);
        int GetExceptionCount();
        void IncrementExceptionCount();
        void SaveExceptionDetails(SkipContext skipContext);
    }
}