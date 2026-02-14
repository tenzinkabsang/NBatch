using Microsoft.Extensions.Logging;
using NBatch.Core.Interfaces;
using NBatch.Core.Repositories;

namespace NBatch.Core;

public sealed class Job
{
    private readonly string _jobName;
    private readonly IReadOnlyDictionary<string, IStep> _steps;
    private readonly IJobRepository _jobRepository;
    private readonly IReadOnlyList<IJobListener> _jobListeners;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<IStepListener>> _stepListeners;
    private readonly ILogger _logger;

    internal Job(
        string jobName,
        IDictionary<string, IStep> steps,
        IJobRepository jobRepository,
        IReadOnlyList<IJobListener> jobListeners,
        Dictionary<string, List<IStepListener>> stepListeners,
        ILogger logger)
    {
        _jobName = jobName;
        _steps = new Dictionary<string, IStep>(steps);
        _jobRepository = jobRepository;
        _jobListeners = jobListeners;
        _stepListeners = stepListeners.ToDictionary(
            kvp => kvp.Key,
            kvp => (IReadOnlyList<IStepListener>)kvp.Value);
        _logger = logger;
    }

    public async Task<JobResult> RunAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Job '{JobName}' starting with {StepCount} step(s)", _jobName, _steps.Count);

        await NotifyJobListenersBeforeAsync(cancellationToken);
        await _jobRepository.CreateJobRecordAsync(_steps.Keys.ToList(), cancellationToken);

        List<StepResult> stepResults = [];
        bool success = true;

        foreach (var (name, step) in _steps)
        {
            cancellationToken.ThrowIfCancellationRequested();

            _logger.LogInformation("Step '{StepName}' starting", name);

            var result = await ExecuteStepAsync(name, step, cancellationToken);
            stepResults.Add(result);
            success &= result.Success;

            _logger.LogInformation(
                "Step '{StepName}' completed — read {ItemsRead}, processed {ItemsProcessed}, skipped {ErrorsSkipped}",
                name, result.ItemsRead, result.ItemsProcessed, result.ErrorsSkipped);
        }

        var jobResult = new JobResult(_jobName, success, stepResults);

        await NotifyJobListenersAfterAsync(jobResult, cancellationToken);

        _logger.LogInformation("Job '{JobName}' completed — success: {Success}", _jobName, success);

        return jobResult;
    }

    private async Task ExecuteStepListenersAsync(
        string stepName,
        Func<IStepListener, Task> action)
    {
        if (_stepListeners.TryGetValue(stepName, out var listeners))
            foreach (var listener in listeners)
                await action(listener);
    }

    private async Task<StepResult> ExecuteStepAsync(string stepName, IStep step, CancellationToken cancellationToken)
    {
        await ExecuteStepListenersAsync(stepName,
            l => l.BeforeStepAsync(stepName, cancellationToken));

        var result = await step.ProcessAsync(cancellationToken);

        await ExecuteStepListenersAsync(stepName,
            l => l.AfterStepAsync(result, cancellationToken));

        return result;
    }

    private async Task NotifyJobListenersBeforeAsync(CancellationToken cancellationToken)
    {
        foreach (var listener in _jobListeners)
            await listener.BeforeJobAsync(_jobName, cancellationToken);
    }

    private async Task NotifyJobListenersAfterAsync(JobResult jobResult, CancellationToken cancellationToken)
    {
        foreach (var listener in _jobListeners)
            await listener.AfterJobAsync(jobResult, cancellationToken);
    }

    public static JobBuilder CreateBuilder(string jobName)
        => new(jobName);
}