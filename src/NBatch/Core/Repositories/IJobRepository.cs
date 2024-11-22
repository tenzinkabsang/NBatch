namespace NBatch.Core.Repositories;

internal interface IJobRepository : IStepRepository
{
    Task<StepContext> GetStartIndexAsync(string stepName);
    Task CreateJobRecordAsync(ICollection<string> stepNames);
}
