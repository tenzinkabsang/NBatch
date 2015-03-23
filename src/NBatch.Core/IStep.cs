using NBatch.Core.Repositories;

namespace NBatch.Core
{
    public interface IStep
    {
        string Name { get; }
        bool Process(long startIndex, IStepRepository stepRepository);
    }
}