using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NBatch.Core.Exceptions;
using NBatch.Core.Interfaces;
using NBatch.Core.Repositories;

namespace NBatch.Core;

/// <summary>
/// Fluent builder for configuring and creating a <see cref="Job"/>.
/// Configuration order does not matter: steps registered before
/// <c>UseJobStore</c> or <see cref="WithLogger"/> still receive the final
/// repository and logger, which are bound when <see cref="Build"/> is called.
/// </summary>
public sealed class JobBuilder
{
    private sealed record StepDefinition(
        string Name,
        Func<IJobRepository, ILogger, IStep> Factory,
        List<IStepListener> Listeners);

    private const int FallbackChunkSize = 10;

    private readonly List<StepDefinition> _stepDefinitions = [];
    private readonly HashSet<string> _stepNames = [];
    private readonly List<IJobListener> _jobListeners = [];
    private IJobRepository? _jobRepository;
    private ILogger _logger = NullLogger.Instance;
    private int? _defaultChunkSize;
    private SkipPolicy? _defaultSkipPolicy;
    private RetryPolicy? _defaultRetryPolicy;

    /// <summary>Gets the name of the job being built.</summary>
    internal string JobName { get; }

    /// <summary>
    /// The per-run service provider for jobs registered via <c>AddNBatch</c>.
    /// Null for standalone <see cref="Job.CreateBuilder"/> jobs, where the
    /// type-based component overloads are unavailable.
    /// </summary>
    internal IServiceProvider? ServiceProvider { get; set; }

    internal JobBuilder(string jobName)
    {
        ArgumentNullException.ThrowIfNull(jobName);
        JobName = jobName;
    }

    internal IServiceProvider RequireServiceProvider(string apiName)
        => ServiceProvider ?? throw new InvalidOperationException(
            $"{apiName} resolves the component from a service provider, which is only available for jobs " +
            "registered via services.AddNBatch(...). For standalone Job.CreateBuilder(...) jobs, pass an instance instead.");

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

    /// <summary>
    /// Sets the chunk size used by steps that don't call
    /// <see cref="IStepBuilderOptions.WithChunkSize"/> themselves. Defaults to 10.
    /// </summary>
    /// <param name="chunkSize">The default number of items per chunk.</param>
    public JobBuilder WithDefaultChunkSize(int chunkSize)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(chunkSize, 1);
        _defaultChunkSize = chunkSize;
        return this;
    }

    /// <summary>
    /// Sets the skip policy used by steps that don't call
    /// <see cref="IStepBuilderOptions.WithSkipPolicy"/> themselves.
    /// </summary>
    /// <param name="skipPolicy">The default skip policy.</param>
    public JobBuilder WithDefaultSkipPolicy(SkipPolicy skipPolicy)
    {
        ArgumentNullException.ThrowIfNull(skipPolicy);
        _defaultSkipPolicy = skipPolicy;
        return this;
    }

    /// <summary>
    /// Sets the retry policy used by steps that don't call
    /// <see cref="IStepBuilderOptions.WithRetryPolicy"/> themselves.
    /// </summary>
    /// <param name="retryPolicy">The default retry policy.</param>
    public JobBuilder WithDefaultRetryPolicy(RetryPolicy retryPolicy)
    {
        ArgumentNullException.ThrowIfNull(retryPolicy);
        _defaultRetryPolicy = retryPolicy;
        return this;
    }

    /// <summary>Adds a named step to the job. Steps execute in registration order.</summary>
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
        Func<IServiceProvider?, IReader<TInput>> readerFactory,
        Func<IServiceProvider?, IWriter<TOutput>> writerFactory,
        Func<IServiceProvider?, IProcessor<TInput, TOutput>>? processorFactory,
        SkipPolicy? skipPolicy,
        RetryPolicy? retryPolicy,
        int? chunkSize,
        List<IStepListener> stepListeners)
    {
        if (!_stepNames.Add(stepName))
            throw new DuplicateStepNameException();

        // Job-level defaults and component factories are evaluated when the
        // definition factory runs (inside Build(), per run for DI jobs), so
        // configuration order doesn't matter and scoped components stay fresh.
        _stepDefinitions.Add(new StepDefinition(
            stepName,
            (repository, logger) => new Step<TInput, TOutput>(
                stepName,
                readerFactory(ServiceProvider),
                processorFactory?.Invoke(ServiceProvider),
                writerFactory(ServiceProvider),
                repository, logger,
                skipPolicy ?? _defaultSkipPolicy,
                retryPolicy ?? _defaultRetryPolicy,
                chunkSize ?? _defaultChunkSize ?? FallbackChunkSize),
            stepListeners));
    }

    internal void RegisterTaskletStep(string stepName, Func<IServiceProvider?, ITasklet> taskletFactory, List<IStepListener> stepListeners)
    {
        if (!_stepNames.Add(stepName))
            throw new DuplicateStepNameException();

        _stepDefinitions.Add(new StepDefinition(
            stepName,
            (repository, logger) => new TaskletStep(stepName, taskletFactory(ServiceProvider), repository, logger),
            stepListeners));
    }

    /// <summary>Creates the configured <see cref="Job"/> instance.</summary>
    /// <exception cref="InvalidOperationException">No steps have been registered.</exception>
    public Job Build()
    {
        if (_stepDefinitions.Count == 0)
            throw new InvalidOperationException(
                $"Job '{JobName}' has no steps. Add at least one step with AddStep(...) before calling Build().");

        var repository = _jobRepository ?? new InMemoryJobRepository(JobName);
        var steps = new List<IStep>(_stepDefinitions.Count);
        var stepListeners = new Dictionary<string, List<IStepListener>>();

        foreach (var definition in _stepDefinitions)
        {
            steps.Add(definition.Factory(repository, _logger));
            if (definition.Listeners.Count > 0)
                stepListeners[definition.Name] = definition.Listeners;
        }

        return new Job(JobName, steps, repository, _jobListeners, stepListeners, _logger);
    }
}
