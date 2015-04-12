
using System.Collections.Generic;

namespace NBatch.Main.Core.Repositories
{
    public interface IJobRepository : IStepRepository
    {
        StepContext GetStartIndex(string stepName);
        void CreateJobRecord(ICollection<string> stepNames);
    }
}