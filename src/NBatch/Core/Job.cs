using NBatch.Core.Interfaces;
using NBatch.Core.Repositories;

namespace NBatch.Core;

public sealed class Job
{
    private readonly string _jobName;
    private readonly IDictionary<string, IStep> _steps;
    private readonly IJobRepository _jobRepository;
    private readonly IReadOnlyList<IJobListener> _jobListeners;
    private readonly Dictionary<string, List<IStepListener>> _stepListeners;

    internal Job(string jobName, IDictionary<string, IStep> steps, IJobRepository jobRepository,
        IReadOnlyList<IJobListener> jobListeners, Dictionary<string, List<IStepListener>> stepListeners)
    {
        _jobName = jobName;
        _steps = steps;
        _jobRepository = jobRepository;
        _jobListeners = jobListeners;
        _stepListeners = stepListeners;
    }

    public async Task<JobResult> RunAsync()
    {
        foreach (var listener in _jobListeners)
            await listener.BeforeJobAsync(_jobName);

        await _jobRepository.CreateJobRecordAsync(_steps.Keys);

        bool success = true;
        List<StepResult> stepResults = [];
        foreach (var (name, step) in _steps)
        {
            StepResult result = await ExecuteStepAsync(name, step);
            stepResults.Add(result);
            success &= result.Success;
        }

        var jobResult = new JobResult(_jobName, success, stepResults);

        foreach (var listener in _jobListeners)
            await listener.AfterJobAsync(jobResult);

        return jobResult;
    }

    private async Task<StepResult> ExecuteStepAsync(string stepName, IStep step)
    {
        _stepListeners.TryGetValue(stepName, out var listeners);

        if (listeners is not null)
            foreach (var listener in listeners)
                await listener.BeforeStepAsync(stepName);

        StepContext context = await _jobRepository.GetStartIndexAsync(stepName);
        var result = await step.ProcessAsync(context, _jobRepository);

        if (listeners is not null)
            foreach (var listener in listeners)
                await listener.AfterStepAsync(result);

        return result;
    }

    public static JobBuilder CreateBuilder(string jobName, string connectionString, DatabaseProvider provider = DatabaseProvider.SqlServer)
        => new(jobName, connectionString, provider);

    public static JobBuilder CreateBuilder(string jobName)
        => new(jobName);
}