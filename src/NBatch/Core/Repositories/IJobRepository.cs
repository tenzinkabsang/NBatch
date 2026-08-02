namespace NBatch.Core.Repositories;

internal interface IJobRepository : IStepRepository
{
    /// <summary>
    /// Records the start of a job run. If the previous run completed successfully,
    /// the job's step progress is reset so the new run starts from the beginning;
    /// otherwise the saved progress is kept so the run resumes.
    /// </summary>
    Task<long> CreateJobRecordAsync(ICollection<string> stepNames, CancellationToken cancellationToken = default);

    /// <summary>Records the outcome of the current run. Drives reset-vs-resume on the next run.</summary>
    Task MarkJobCompleteAsync(bool success, CancellationToken cancellationToken = default);
}
