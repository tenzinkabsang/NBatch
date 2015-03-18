
namespace NBatch.Core.Repositories
{
    public interface IJobRepository : IStepRepository
    {
        int GetStartIndex(string stepName);
    }
}