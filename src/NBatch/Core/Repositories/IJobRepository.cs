namespace NBatch.Core.Repositories;

internal interface IJobRepository : IStepRepository
{
    Task<long> CreateJobRecordAsync(ICollection<string> stepNames, CancellationToken cancellationToken = default);
}
