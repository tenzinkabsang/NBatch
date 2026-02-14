namespace NBatch.Core.Repositories;

internal interface IJobRepository : IStepRepository
{
    Task CreateJobRecordAsync(ICollection<string> stepNames, CancellationToken cancellationToken = default);
}
