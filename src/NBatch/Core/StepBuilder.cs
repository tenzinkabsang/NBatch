using NBatch.Core.Interfaces;

namespace NBatch.Core;

internal interface IStepRegistration
{
    void Register();
}

// Step builders carry component FACTORIES rather than instances: instance and
// delegate overloads wrap as `_ => instance`, while the type-based overloads
// resolve from the per-run service provider. Factories are invoked inside
// JobBuilder.Build(), which runs per job run within the DI scope — so scoped
// services (e.g. DbContext) are fresh on every run.

internal sealed class StepBuilderReadFrom(JobBuilder jobBuilder, string stepName) : IStepBuilderReadFrom
{
    public IStepBuilderProcess<TInput> ReadFrom<TInput>(IReader<TInput> reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        return new StepBuilderProcess<TInput>(jobBuilder, stepName, _ => reader);
    }

    public IStepBuilderProcess<TItem> ReadFrom<TReader, TItem>() where TReader : class, IReader<TItem>
    {
        jobBuilder.RequireServiceProvider("ReadFrom<TReader, TItem>()");
        return new StepBuilderProcess<TItem>(jobBuilder, stepName, sp => ComponentResolver.Resolve<TReader>(sp!));
    }

    public ITaskletStepBuilder Execute(ITasklet tasklet)
    {
        ArgumentNullException.ThrowIfNull(tasklet);
        return new TaskletStepBuilder(jobBuilder, stepName, _ => tasklet);
    }

    public ITaskletStepBuilder Execute<TTasklet>() where TTasklet : class, ITasklet
    {
        jobBuilder.RequireServiceProvider("Execute<TTasklet>()");
        return new TaskletStepBuilder(jobBuilder, stepName, sp => ComponentResolver.Resolve<TTasklet>(sp!));
    }

    public ITaskletStepBuilder Execute(Func<Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        return new TaskletStepBuilder(jobBuilder, stepName, _ => new DelegateTasklet(_ => action()));
    }

    public ITaskletStepBuilder Execute(Func<CancellationToken, Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        return new TaskletStepBuilder(jobBuilder, stepName, _ => new DelegateTasklet(action));
    }

    public ITaskletStepBuilder Execute(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        return new TaskletStepBuilder(jobBuilder, stepName, _ => new DelegateTasklet(_ => { action(); return Task.CompletedTask; }));
    }
}

internal sealed class StepBuilderProcess<TInput>(
    JobBuilder jobBuilder,
    string stepName,
    Func<IServiceProvider?, IReader<TInput>> readerFactory) : IStepBuilderProcess<TInput>
{
    public IStepBuilderWriteTo<TOutput> ProcessWith<TOutput>(IProcessor<TInput, TOutput> processor)
    {
        ArgumentNullException.ThrowIfNull(processor);
        return new StepBuilderWriteTo<TInput, TOutput>(jobBuilder, stepName, readerFactory, _ => processor);
    }

    public IStepBuilderWriteTo<TOutput> ProcessWith<TOutput>(Func<TInput, TOutput> processor)
    {
        ArgumentNullException.ThrowIfNull(processor);
        return new StepBuilderWriteTo<TInput, TOutput>(jobBuilder, stepName, readerFactory,
            _ => new DelegateProcessor<TInput, TOutput>(processor));
    }

    public IStepBuilderWriteTo<TOutput> ProcessWith<TOutput>(Func<TInput, CancellationToken, Task<TOutput>> processor)
    {
        ArgumentNullException.ThrowIfNull(processor);
        return new StepBuilderWriteTo<TInput, TOutput>(jobBuilder, stepName, readerFactory,
            _ => new DelegateProcessor<TInput, TOutput>(processor));
    }

    public IStepBuilderWriteTo<TOutput> ProcessWith<TProcessor, TOutput>() where TProcessor : class, IProcessor<TInput, TOutput>
    {
        jobBuilder.RequireServiceProvider("ProcessWith<TProcessor, TOutput>()");
        return new StepBuilderWriteTo<TInput, TOutput>(jobBuilder, stepName, readerFactory,
            sp => ComponentResolver.Resolve<TProcessor>(sp!));
    }

    public IStepBuilderOptions WriteTo(IWriter<TInput> writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        return new StepBuilderOptions<TInput, TInput>(jobBuilder, stepName, readerFactory, null, _ => writer);
    }

    public IStepBuilderOptions WriteTo(Func<IEnumerable<TInput>, Task> writeAction)
    {
        ArgumentNullException.ThrowIfNull(writeAction);
        return new StepBuilderOptions<TInput, TInput>(jobBuilder, stepName, readerFactory, null,
            _ => new DelegateWriter<TInput>(writeAction));
    }

    public IStepBuilderOptions WriteTo(Func<IEnumerable<TInput>, CancellationToken, Task> writeAction)
    {
        ArgumentNullException.ThrowIfNull(writeAction);
        return new StepBuilderOptions<TInput, TInput>(jobBuilder, stepName, readerFactory, null,
            _ => new DelegateWriter<TInput>(writeAction));
    }

    public IStepBuilderOptions WriteTo<TWriter>() where TWriter : class, IWriter<TInput>
    {
        jobBuilder.RequireServiceProvider("WriteTo<TWriter>()");
        return new StepBuilderOptions<TInput, TInput>(jobBuilder, stepName, readerFactory, null,
            sp => ComponentResolver.Resolve<TWriter>(sp!));
    }
}

internal sealed class StepBuilderWriteTo<TInput, TOutput>(
    JobBuilder jobBuilder,
    string stepName,
    Func<IServiceProvider?, IReader<TInput>> readerFactory,
    Func<IServiceProvider?, IProcessor<TInput, TOutput>> processorFactory) : IStepBuilderWriteTo<TOutput>
{
    public IStepBuilderOptions WriteTo(IWriter<TOutput> writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        return new StepBuilderOptions<TInput, TOutput>(jobBuilder, stepName, readerFactory, processorFactory, _ => writer);
    }

    public IStepBuilderOptions WriteTo(Func<IEnumerable<TOutput>, Task> writeAction)
    {
        ArgumentNullException.ThrowIfNull(writeAction);
        return new StepBuilderOptions<TInput, TOutput>(jobBuilder, stepName, readerFactory, processorFactory,
            _ => new DelegateWriter<TOutput>(writeAction));
    }

    public IStepBuilderOptions WriteTo(Func<IEnumerable<TOutput>, CancellationToken, Task> writeAction)
    {
        ArgumentNullException.ThrowIfNull(writeAction);
        return new StepBuilderOptions<TInput, TOutput>(jobBuilder, stepName, readerFactory, processorFactory,
            _ => new DelegateWriter<TOutput>(writeAction));
    }

    public IStepBuilderOptions WriteTo<TWriter>() where TWriter : class, IWriter<TOutput>
    {
        jobBuilder.RequireServiceProvider("WriteTo<TWriter>()");
        return new StepBuilderOptions<TInput, TOutput>(jobBuilder, stepName, readerFactory, processorFactory,
            sp => ComponentResolver.Resolve<TWriter>(sp!));
    }
}

internal sealed class StepBuilderOptions<TInput, TOutput>(
    JobBuilder jobBuilder,
    string stepName,
    Func<IServiceProvider?, IReader<TInput>> readerFactory,
    Func<IServiceProvider?, IProcessor<TInput, TOutput>>? processorFactory,
    Func<IServiceProvider?, IWriter<TOutput>> writerFactory) : IStepBuilderOptions, IStepRegistration
{
    private SkipPolicy? _skipPolicy;
    private RetryPolicy? _retryPolicy;
    private readonly List<IStepListener> _stepListeners = [];
    private int? _chunkSize;
    private bool _registered;

    public IStepBuilderOptions WithSkipPolicy(SkipPolicy skipPolicy)
    {
        ArgumentNullException.ThrowIfNull(skipPolicy);
        _skipPolicy = skipPolicy;
        return this;
    }

    public IStepBuilderOptions WithRetryPolicy(RetryPolicy retryPolicy)
    {
        ArgumentNullException.ThrowIfNull(retryPolicy);
        _retryPolicy = retryPolicy;
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
        jobBuilder.RegisterStep(stepName, readerFactory, writerFactory, processorFactory, _skipPolicy, _retryPolicy, _chunkSize, _stepListeners);
    }

    void IStepRegistration.Register() => RegisterStep();
}

internal sealed class TaskletStepBuilder(
    JobBuilder jobBuilder,
    string stepName,
    Func<IServiceProvider?, ITasklet> taskletFactory) : ITaskletStepBuilder, IStepRegistration
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
        jobBuilder.RegisterTaskletStep(stepName, taskletFactory, _stepListeners);
    }

    void IStepRegistration.Register() => RegisterStep();
}
