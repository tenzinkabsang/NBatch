using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NBatch.Core.Exceptions;
using NBatch.Core.Interfaces;
using NBatch.Core.Repositories;

namespace NBatch.Core;

public sealed class JobBuilder
{
    private readonly string _jobName;
    private IJobRepository _jobRepository;
    private readonly Dictionary<string, IStep> _steps = [];
    private readonly Dictionary<string, List<IStepListener>> _stepListeners = [];
    private readonly List<IJobListener> _jobListeners = [];
    private ILogger _logger = NullLogger.Instance;

    internal JobBuilder(string jobName)
    {
        ArgumentNullException.ThrowIfNull(jobName);
        _jobName = jobName;
        _jobRepository = new InMemoryJobRepository(jobName);
    }

    public JobBuilder UseJobStore(string connectionString, DatabaseProvider provider = DatabaseProvider.SqlServer)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        _jobRepository = new EfJobRepository(_jobName, connectionString, provider);
        return this;
    }

    public JobBuilder WithLogger(ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
        return this;
    }

    public JobBuilder WithListener(IJobListener listener)
    {
        ArgumentNullException.ThrowIfNull(listener);
        _jobListeners.Add(listener);
        return this;
    }

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

    public Job Build() => new(_jobName, _steps, _jobRepository, _jobListeners, _stepListeners, _logger);
}
