using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NBatch.Core.Exceptions;
using NBatch.Core.Interfaces;
using NBatch.Core.Repositories;

namespace NBatch.Core;

/// <summary>
/// Fluent builder for configuring and creating a <see cref="Job"/>.
/// </summary>
public sealed class JobBuilder
{
    private IJobRepository _jobRepository;
    private readonly Dictionary<string, IStep> _steps = [];
    private readonly Dictionary<string, List<IStepListener>> _stepListeners = [];
    private readonly List<IJobListener> _jobListeners = [];
    private ILogger _logger = NullLogger.Instance;

    /// <summary>Gets the name of the job being built.</summary>
    internal string JobName { get; }

    internal JobBuilder(string jobName)
    {
        ArgumentNullException.ThrowIfNull(jobName);
        JobName = jobName;
        _jobRepository = new InMemoryJobRepository(jobName);
    }

    /// <summary>
    /// Sets the job repository implementation.
    /// Used by provider packages (e.g. <c>NBatch.EntityFrameworkCore</c>) to inject
    /// a persistent job store.
    /// </summary>
    internal void SetJobRepository(IJobRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        _jobRepository = repository;
    }

    /// <summary>Sets the logger used for job and step diagnostics.</summary>
    /// <param name="logger">The logger instance.</param>
    public JobBuilder WithLogger(ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
        return this;
    }

    /// <summary>Registers a job-level listener for before/after callbacks.</summary>
    /// <param name="listener">The listener to register.</param>
    public JobBuilder WithListener(IJobListener listener)
    {
        ArgumentNullException.ThrowIfNull(listener);
        _jobListeners.Add(listener);
        return this;
    }

    /// <summary>Adds a named step to the job.</summary>
    /// <param name="stepName">A unique name for this step.</param>
    /// <param name="configure">A delegate that configures the step pipeline.</param>
    public JobBuilder AddStep(string stepName, Func<IStepBuilderReadFrom, IStepBuilderFinal> configure)
    {
        ArgumentNullException.ThrowIfNull(stepName);
        ArgumentNullException.ThrowIfNull(configure);
        var readFrom = new StepBuilderReadFrom(this, stepName);
        var result = configure(readFrom);
        if (result is IStepRegistration registration)
            registration.Register();
        return this;
    }

    internal void RegisterStep<TInput, TOutput>(
        string stepName,
        IReader<TInput> reader,
        IWriter<TOutput> writer,
        IProcessor<TInput, TOutput>? processor,
        SkipPolicy? skipPolicy,
        int chunkSize,
        List<IStepListener> stepListeners)
    {
        if (_steps.ContainsKey(stepName))
            throw new DuplicateStepNameException();

        var step = new Step<TInput, TOutput>(stepName, reader, processor, writer, _jobRepository, _logger, skipPolicy, chunkSize);
        _steps.Add(step.Name, step);
        if (stepListeners.Count > 0)
            _stepListeners[stepName] = stepListeners;
    }

    internal void RegisterTaskletStep(string stepName, ITasklet tasklet, List<IStepListener> stepListeners)
    {
        if (_steps.ContainsKey(stepName))
            throw new DuplicateStepNameException();

        var step = new TaskletStep(stepName, tasklet, _jobRepository, _logger);
        _steps.Add(step.Name, step);
        if (stepListeners.Count > 0)
            _stepListeners[stepName] = stepListeners;
    }

    /// <summary>Creates the configured <see cref="Job"/> instance.</summary>
    public Job Build() => new(JobName, _steps, _jobRepository, _jobListeners, _stepListeners, _logger);
}
