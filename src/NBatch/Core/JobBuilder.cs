using NBatch.Core.Exceptions;
using NBatch.Core.Interfaces;
using NBatch.Core.Repositories;

namespace NBatch.Core;

public sealed class JobBuilder
{
    private readonly string _jobName;
    private readonly IJobRepository _jobRepository;
    private readonly Dictionary<string, IStep> _steps = [];
    private readonly Dictionary<string, List<IStepListener>> _stepListeners = [];
    private readonly List<IJobListener> _jobListeners = [];

    internal JobBuilder(string jobName, string connectionString, DatabaseProvider provider)
    {
        ArgumentNullException.ThrowIfNull(jobName);
        ArgumentNullException.ThrowIfNull(connectionString);
        _jobName = jobName;
        _jobRepository = new EfJobRepository(jobName, connectionString, provider);
    }

    internal JobBuilder(string jobName)
    {
        ArgumentNullException.ThrowIfNull(jobName);
        _jobName = jobName;
        _jobRepository = new InMemoryJobRepository(jobName);
    }

    public JobBuilder WithListener(IJobListener listener)
    {
        ArgumentNullException.ThrowIfNull(listener);
        _jobListeners.Add(listener);
        return this;
    }

    public IStepBuilderReadFrom AddStep(string stepName)
    {
        ArgumentNullException.ThrowIfNull(stepName);
        return new StepBuilderReadFrom(this, stepName);
    }

    internal void RegisterStep<TInput, TOutput>(
        string stepName,
        IReader<TInput> reader,
        IWriter<TOutput> writer,
        IProcessor<TInput, TOutput>? processor,
        SkipPolicy? skipPolicy,
        RetryPolicy? retryPolicy,
        int chunkSize,
        List<IStepListener> stepListeners)
    {
        if (_steps.ContainsKey(stepName))
            throw new DuplicateStepNameException();

        var step = new Step<TInput, TOutput>(stepName, reader, processor, writer, skipPolicy, retryPolicy, chunkSize);
        _steps.Add(step.Name, step);
        if (stepListeners.Count > 0)
            _stepListeners[stepName] = stepListeners;
    }

    internal void RegisterTaskletStep(string stepName, ITasklet tasklet, List<IStepListener> stepListeners)
    {
        if (_steps.ContainsKey(stepName))
            throw new DuplicateStepNameException();

        var step = new TaskletStep(stepName, tasklet);
        _steps.Add(step.Name, step);
        if (stepListeners.Count > 0)
            _stepListeners[stepName] = stepListeners;
    }

    public Job Build() => new(_jobName, _steps, _jobRepository, _jobListeners, _stepListeners);
}
