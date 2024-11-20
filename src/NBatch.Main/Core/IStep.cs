
using NBatch.Main.Core.Repositories;

namespace NBatch.Main.Core;

public interface IStep
{
    string Name { get; }
    bool Process(StepContext stepContext, IStepRepository stepRepository);
}