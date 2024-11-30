using NBatch.Core.Interfaces;
using NBatch.Core.Repositories;

namespace NBatch.Core;

public sealed class Job
{
    private readonly string _jobName;
    private readonly IJobRepository _jobRepository;
    private readonly IDictionary<string, IStep> _steps;

    internal Job(string jobName, IDictionary<string, IStep> steps, IJobRepository jobRepository)
    {
        _jobName = jobName;
        _steps = steps;
        _jobRepository = jobRepository;
    }

    public async Task<JobResult> RunAsync()
    {
        await _jobRepository.CreateJobRecordAsync(_steps.Keys);
        bool success = true;
        foreach (var step in _steps)
        {
            StepContext stepContext = await _jobRepository.GetStartIndexAsync(step.Key);
            success &= (await step.Value.ProcessAsync(stepContext, _jobRepository)).Success;
        }
        return new JobResult(_jobName, success);
    }

    public static JobBuilder CreateBuilder(string jobName, string connectionString)
        => new(jobName, connectionString);
}