using NBatch.Core.Repositories;

namespace NBatch.Core.Interfaces;

public interface IStep
{
    string Name { get; }
    Task<bool> ProcessAsync(StepContext stepContext, IStepRepository stepRepository);
}
