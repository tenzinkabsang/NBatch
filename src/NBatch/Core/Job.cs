using Microsoft.Extensions.Logging;
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
    private readonly ILogger _logger;

    internal Job(string jobName, IDictionary<string, IStep> steps, IJobRepository jobRepository,
        IReadOnlyList<IJobListener> jobListeners, Dictionary<string, List<IStepListener>> stepListeners,
        ILogger logger)
    {
        _jobName = jobName;
        _steps = steps;
        _jobRepository = jobRepository;
        _jobListeners = jobListeners;
        _stepListeners = stepListeners;
        _logger = logger;
    }

    public async Task<JobResult> RunAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Job '{JobName}' starting with {StepCount} step(s)", _jobName, _steps.Count);

        foreach (var listener in _jobListeners)
            await listener.BeforeJobAsync(_jobName, cancellationToken);

        await _jobRepository.CreateJobRecordAsync(_steps.Keys, cancellationToken);

        bool success = true;
        List<StepResult> stepResults = [];
        foreach (var (name, step) in _steps)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _logger.LogInformation("Step '{StepName}' starting", name);
            StepResult result = await ExecuteStepAsync(name, step, cancellationToken);
            stepResults.Add(result);
            success &= result.Success;
            _logger.LogInformation("Step '{StepName}' completed — read {ItemsRead}, processed {ItemsProcessed}, skipped {ErrorsSkipped}",
                name, result.ItemsRead, result.ItemsProcessed, result.ErrorsSkipped);
        }

        var jobResult = new JobResult(_jobName, success, stepResults);

        foreach (var listener in _jobListeners)
            await listener.AfterJobAsync(jobResult, cancellationToken);

        _logger.LogInformation("Job '{JobName}' completed — success: {Success}", _jobName, success);

        return jobResult;
    }

    private async Task<StepResult> ExecuteStepAsync(string stepName, IStep step, CancellationToken cancellationToken)
    {
        _stepListeners.TryGetValue(stepName, out var listeners);

        if (listeners is not null)
            foreach (var listener in listeners)
                await listener.BeforeStepAsync(stepName, cancellationToken);

        var result = await step.ProcessAsync(cancellationToken);

        if (listeners is not null)
            foreach (var listener in listeners)
                await listener.AfterStepAsync(result, cancellationToken);

        return result;
    }

    public static JobBuilder CreateBuilder(string jobName, string connectionString, DatabaseProvider provider = DatabaseProvider.SqlServer)
        => new(jobName, connectionString, provider);

    public static JobBuilder CreateBuilder(string jobName)
        => new(jobName);
}