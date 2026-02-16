using NBatch.Core.Interfaces;

namespace NBatch.Core;

internal interface IStepRegistration
{
    void Register();
}

internal sealed class StepBuilderReadFrom(JobBuilder jobBuilder, string stepName) : IStepBuilderReadFrom
{
    public IStepBuilderProcess<TInput> ReadFrom<TInput>(IReader<TInput> reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        return new StepBuilderProcess<TInput>(jobBuilder, stepName, reader);
    }

    public ITaskletStepBuilder Execute(ITasklet tasklet)
    {
        ArgumentNullException.ThrowIfNull(tasklet);
        return new TaskletStepBuilder(jobBuilder, stepName, tasklet);
    }

    public ITaskletStepBuilder Execute(Func<Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        return new TaskletStepBuilder(jobBuilder, stepName, new DelegateTasklet(_ => action()));
    }

    public ITaskletStepBuilder Execute(Func<CancellationToken, Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        return new TaskletStepBuilder(jobBuilder, stepName, new DelegateTasklet(action));
    }

    public ITaskletStepBuilder Execute(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        return new TaskletStepBuilder(jobBuilder, stepName, new DelegateTasklet(_ => { action(); return Task.CompletedTask; }));
    }
}

internal sealed class StepBuilderProcess<TInput>(JobBuilder jobBuilder, string stepName, IReader<TInput> reader) : IStepBuilderProcess<TInput>
{
    public IStepBuilderWriteTo<TOutput> ProcessWith<TOutput>(IProcessor<TInput, TOutput> processor)
    {
        ArgumentNullException.ThrowIfNull(processor);
        return new StepBuilderWriteTo<TInput, TOutput>(jobBuilder, stepName, reader, processor);
    }

    public IStepBuilderWriteTo<TOutput> ProcessWith<TOutput>(Func<TInput, TOutput> processor)
    {
        ArgumentNullException.ThrowIfNull(processor);
        return new StepBuilderWriteTo<TInput, TOutput>(jobBuilder, stepName, reader, new DelegateProcessor<TInput, TOutput>(processor));
    }

    public IStepBuilderWriteTo<TOutput> ProcessWith<TOutput>(Func<TInput, CancellationToken, Task<TOutput>> processor)
    {
        ArgumentNullException.ThrowIfNull(processor);
        return new StepBuilderWriteTo<TInput, TOutput>(jobBuilder, stepName, reader, new DelegateProcessor<TInput, TOutput>(processor));
    }

    public IStepBuilderOptions WriteTo(IWriter<TInput> writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        return new StepBuilderOptions<TInput, TInput>(jobBuilder, stepName, reader, null, writer);
    }

    public IStepBuilderOptions WriteTo(Func<IEnumerable<TInput>, Task> writeAction)
    {
        ArgumentNullException.ThrowIfNull(writeAction);
        return new StepBuilderOptions<TInput, TInput>(jobBuilder, stepName, reader, null, new DelegateWriter<TInput>(writeAction));
    }

    public IStepBuilderOptions WriteTo(Func<IEnumerable<TInput>, CancellationToken, Task> writeAction)
    {
        ArgumentNullException.ThrowIfNull(writeAction);
        return new StepBuilderOptions<TInput, TInput>(jobBuilder, stepName, reader, null, new DelegateWriter<TInput>(writeAction));
    }
}

internal sealed class StepBuilderWriteTo<TInput, TOutput>(
    JobBuilder jobBuilder,
    string stepName,
    IReader<TInput> reader,
    IProcessor<TInput, TOutput> processor) : IStepBuilderWriteTo<TOutput>
{
    public IStepBuilderOptions WriteTo(IWriter<TOutput> writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        return new StepBuilderOptions<TInput, TOutput>(jobBuilder, stepName, reader, processor, writer);
    }

    public IStepBuilderOptions WriteTo(Func<IEnumerable<TOutput>, Task> writeAction)
    {
        ArgumentNullException.ThrowIfNull(writeAction);
        return new StepBuilderOptions<TInput, TOutput>(jobBuilder, stepName, reader, processor, new DelegateWriter<TOutput>(writeAction));
    }

    public IStepBuilderOptions WriteTo(Func<IEnumerable<TOutput>, CancellationToken, Task> writeAction)
    {
        ArgumentNullException.ThrowIfNull(writeAction);
        return new StepBuilderOptions<TInput, TOutput>(jobBuilder, stepName, reader, processor, new DelegateWriter<TOutput>(writeAction));
    }
}

internal sealed class StepBuilderOptions<TInput, TOutput>(
    JobBuilder jobBuilder,
    string stepName,
    IReader<TInput> reader,
    IProcessor<TInput, TOutput>? processor,
    IWriter<TOutput> writer) : IStepBuilderOptions, IStepRegistration
{
    private SkipPolicy? _skipPolicy;
    private readonly List<IStepListener> _stepListeners = [];
    private int _chunkSize = 10;
    private bool _registered;

    public IStepBuilderOptions WithSkipPolicy(SkipPolicy skipPolicy)
    {
        ArgumentNullException.ThrowIfNull(skipPolicy);
        _skipPolicy = skipPolicy;
        return this;
    }

    public IStepBuilderOptions WithListener(IStepListener listener)
    {
        ArgumentNullException.ThrowIfNull(listener);
        _stepListeners.Add(listener);
        return this;
    }

    public IStepBuilderOptions WithChunkSize(int chunkSize)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(chunkSize, 1);
        _chunkSize = chunkSize;
        return this;
    }

    private void RegisterStep()
    {
        if (_registered) return;
        _registered = true;
        jobBuilder.RegisterStep(stepName, reader, writer, processor, _skipPolicy, _chunkSize, _stepListeners);
    }

    void IStepRegistration.Register() => RegisterStep();
}

internal sealed class TaskletStepBuilder(JobBuilder jobBuilder, string stepName, ITasklet tasklet) : ITaskletStepBuilder, IStepRegistration
{
    private readonly List<IStepListener> _stepListeners = [];
    private bool _registered;

    public ITaskletStepBuilder WithListener(IStepListener listener)
    {
        ArgumentNullException.ThrowIfNull(listener);
        _stepListeners.Add(listener);
        return this;
    }

    private void RegisterStep()
    {
        if (_registered) return;
        _registered = true;
        jobBuilder.RegisterTaskletStep(stepName, tasklet, _stepListeners);
    }

    void IStepRegistration.Register() => RegisterStep();
}
