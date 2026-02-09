using NBatch.Core.Interfaces;
using NBatch.Core.Repositories;

namespace NBatch.Core;

public sealed class Job
{
    private readonly string _jobName;
    private readonly IDictionary<string, IStep> _steps;
    private readonly IJobRepository _jobRepository;

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
        foreach (var (name, step) in _steps)
        {
            StepResult result = await ExecuteStepAsync(name, step);
            success &= result.Success;
        }

        return new JobResult(_jobName, success);
    }

    private async Task<StepResult> ExecuteStepAsync(string stepName, IStep step)
    {
        StepContext context = await _jobRepository.GetStartIndexAsync(stepName);
        return await step.ProcessAsync(context, _jobRepository);
    }

    public static JobBuilder CreateBuilder(string jobName, string connectionString)
        => new(jobName, connectionString);
}