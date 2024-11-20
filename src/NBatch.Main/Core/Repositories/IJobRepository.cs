
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NBatch.Main.Core.Repositories;

public interface IJobRepository : IStepRepository
{
    Task<StepContext> GetStartIndexAsync(string stepName);
    Task CreateJobRecord(ICollection<string> stepNames);
}