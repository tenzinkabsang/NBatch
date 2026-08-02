using System.Runtime.ExceptionServices;
using Microsoft.Extensions.Logging;
using NBatch.Core.Interfaces;
using NBatch.Core.Repositories;

namespace NBatch.Core;

/// <summary>
/// A configured batch job containing one or more steps executed in sequence.
/// Create instances via <see cref="CreateBuilder"/>.
/// </summary>
public sealed class Job
{
    private readonly string _jobName;
    private readonly IReadOnlyList<IStep> _steps;
    private readonly IJobRepository _jobRepository;
    private readonly IReadOnlyList<IJobListener> _jobListeners;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<IStepListener>> _stepListeners;
    private readonly ILogger _logger;

    internal Job(
        string jobName,
        IReadOnlyList<IStep> steps,
        IJobRepository jobRepository,
        IReadOnlyList<IJobListener> jobListeners,
        Dictionary<string, List<IStepListener>> stepListeners,
        ILogger logger)
    {
        _jobName = jobName;
        _steps = steps;
        _jobRepository = jobRepository;
        _jobListeners = jobListeners;
        _stepListeners = stepListeners.ToDictionary(
            kvp => kvp.Key,
            kvp => (IReadOnlyList<IStepListener>)kvp.Value);
        _logger = logger;
    }

    /// <summary>
    /// Executes all steps in order and returns the aggregate result.
    /// If the previous run completed successfully, step progress is reset and the
    /// job starts from the beginning; after a failure, crash, or cancellation the
    /// job resumes from where it left off (when a persistent job store is used).
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the job.</param>
    /// <exception cref="OperationCanceledException">The run was cancelled. Job listeners
    /// still receive an <see cref="JobResult"/> with <see cref="JobResult.Cancelled"/> set.</exception>
    public async Task<JobResult> RunAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Job '{JobName}' starting with {StepCount} step(s)", _jobName, _steps.Count);

        await NotifyJobListenersBeforeAsync(cancellationToken);
        _ = await _jobRepository.CreateJobRecordAsync(_steps.Select(s => s.Name).ToList(), cancellationToken);

        List<StepResult> stepResults = [];
        ExceptionDispatchInfo? cancellation = null;

        try
        {
            foreach (var step in _steps)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var result = await ExecuteStepAsync(step.Name, step, cancellationToken);

                stepResults.Add(result);

                if (!result.Success)
                    break;
            }
        }
        catch (OperationCanceledException ex)
        {
            cancellation = ExceptionDispatchInfo.Capture(ex);
        }

        bool cancelled = cancellation is not null;
        bool success = !cancelled && stepResults.TrueForAll(r => r.Success);
        var jobResult = new JobResult(_jobName, success, stepResults, cancelled);

        // Bookkeeping must survive a cancelled token: the outcome drives
        // reset-vs-resume on the next run.
        await _jobRepository.MarkJobCompleteAsync(success, CancellationToken.None);

        await NotifyJobListenersAfterAsync(jobResult, cancelled ? CancellationToken.None : cancellationToken);

        if (cancelled)
        {
            _logger.LogWarning("Job '{JobName}' was cancelled", _jobName);
            cancellation!.Throw();
        }

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
        _logger.LogInformation("Step '{StepName}' starting", stepName);

        await ExecuteStepListenersAsync(stepName,
            l => l.BeforeStepAsync(stepName, cancellationToken));

        StepResult result;
        try
        {
            result = await step.ProcessAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Step '{StepName}' failed", stepName);
            result = new StepResult(stepName, false);
        }

        await ExecuteStepListenersAsync(stepName,
            l => l.AfterStepAsync(result, cancellationToken));

        _logger.LogInformation(
            "Step '{StepName}' completed — read {ItemsRead}, processed {ItemsProcessed}, skipped {ErrorsSkipped}",
            stepName, result.ItemsRead, result.ItemsProcessed, result.ErrorsSkipped);

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

    /// <summary>Creates a new <see cref="JobBuilder"/> for configuring a job.</summary>
    /// <param name="jobName">A unique name that identifies this job.</param>
    public static JobBuilder CreateBuilder(string jobName)
        => new(jobName);
}
