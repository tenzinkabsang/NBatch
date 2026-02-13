using NBatch.Core.Interfaces;

namespace NBatch.Core;

/// <summary>
/// First stage: provide a reader for chunk-oriented processing, or a tasklet for single-unit work.
/// </summary>
public interface IStepBuilderReadFrom
{
    IStepBuilderWriteTo<TInput> ReadFrom<TInput>(IReader<TInput> reader);
    ITaskletStepBuilder Execute(ITasklet tasklet);
    ITaskletStepBuilder Execute(Func<Task> action);
}

/// <summary>
/// Second stage: a writer must be provided.
/// </summary>
public interface IStepBuilderWriteTo<TInput>
{
    IStepBuilderOptions<TInput, TOutput> WriteTo<TOutput>(IWriter<TOutput> writer);
}

/// <summary>
/// Final stage: configure optional settings, add another step, or build the job.
/// </summary>
public interface IStepBuilderOptions<TInput, TOutput>
{
    IStepBuilderOptions<TInput, TOutput> ProcessWith(IProcessor<TInput, TOutput> processor);
    IStepBuilderOptions<TInput, TOutput> ProcessWith(Func<TInput, TOutput> processor);
    IStepBuilderOptions<TInput, TOutput> WithSkipPolicy(SkipPolicy skipPolicy);
    IStepBuilderOptions<TInput, TOutput> WithRetryPolicy(RetryPolicy retryPolicy);
    IStepBuilderOptions<TInput, TOutput> WithListener(IStepListener listener);
    IStepBuilderOptions<TInput, TOutput> WithChunkSize(int chunkSize);
    IStepBuilderReadFrom AddStep(string stepName);
    Job Build();
}

/// <summary>
/// Terminal stage for a tasklet step: add another step, attach a listener, or build the job.
/// </summary>
public interface ITaskletStepBuilder
{
    ITaskletStepBuilder WithListener(IStepListener listener);
    IStepBuilderReadFrom AddStep(string stepName);
    Job Build();
}
