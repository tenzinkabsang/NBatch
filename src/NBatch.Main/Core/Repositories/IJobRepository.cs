
using System.Collections.Generic;

namespace NBatch.Main.Core.Repositories
{
    public interface IJobRepository : IStepRepository
    {
        long GetStartIndex(string stepName);
        void CreateJobRecord(ICollection<string> stepNames);
    }
}