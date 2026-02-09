using NBatch.Core.Interfaces;

namespace NBatch.Core;

internal sealed class StepBuilderReadFrom(JobBuilder jobBuilder, string stepName) : IStepBuilderReadFrom
{
    public IStepBuilderWriteTo<TInput> ReadFrom<TInput>(IReader<TInput> reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        return new StepBuilderWriteTo<TInput>(jobBuilder, stepName, reader);
    }
}

internal sealed class StepBuilderWriteTo<TInput>(JobBuilder jobBuilder, string stepName, IReader<TInput> reader) : IStepBuilderWriteTo<TInput>
{
    public IStepBuilderOptions<TInput, TOutput> WriteTo<TOutput>(IWriter<TOutput> writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        return new StepBuilderOptions<TInput, TOutput>(jobBuilder, stepName, reader, writer);
    }
}

internal sealed class StepBuilderOptions<TInput, TOutput>(
    JobBuilder jobBuilder,
    string stepName,
    IReader<TInput> reader,
    IWriter<TOutput> writer) : IStepBuilderOptions<TInput, TOutput>
{
    private IProcessor<TInput, TOutput>? _processor;
    private SkipPolicy? _skipPolicy;
    private int _chunkSize = 10;
    private bool _registered;

    public IStepBuilderOptions<TInput, TOutput> ProcessWith(IProcessor<TInput, TOutput> processor)
    {
        ArgumentNullException.ThrowIfNull(processor);
        _processor = processor;
        return this;
    }

    public IStepBuilderOptions<TInput, TOutput> ProcessWith(Func<TInput, TOutput> processor)
    {
        ArgumentNullException.ThrowIfNull(processor);
        _processor = new DelegateProcessor<TInput, TOutput>(processor);
        return this;
    }

    public IStepBuilderOptions<TInput, TOutput> WithSkipPolicy(SkipPolicy skipPolicy)
    {
        ArgumentNullException.ThrowIfNull(skipPolicy);
        _skipPolicy = skipPolicy;
        return this;
    }

    public IStepBuilderOptions<TInput, TOutput> WithChunkSize(int chunkSize)
    {
        _chunkSize = chunkSize;
        return this;
    }

    public IStepBuilderReadFrom AddStep(string stepName)
    {
        RegisterStep();
        return jobBuilder.AddStep(stepName);
    }

    public Job Build()
    {
        RegisterStep();
        return jobBuilder.Build();
    }

    private void RegisterStep()
    {
        if (_registered) return;
        _registered = true;
        jobBuilder.RegisterStep(stepName, reader, writer, _processor, _skipPolicy, _chunkSize);
    }
}
