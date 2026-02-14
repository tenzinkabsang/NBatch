using NBatch.Core.Interfaces;

namespace NBatch.Core;

/// <summary>
/// First stage: provide a reader for chunk-oriented processing, or a tasklet for single-unit work.
/// </summary>
public interface IStepBuilderReadFrom
{
    IStepBuilderProcess<TInput> ReadFrom<TInput>(IReader<TInput> reader);
    ITaskletStepBuilder Execute(ITasklet tasklet);
    ITaskletStepBuilder Execute(Func<Task> action);
    ITaskletStepBuilder Execute(Func<CancellationToken, Task> action);
}

/// <summary>
/// Second stage: optionally process the items, or skip straight to writing.
/// </summary>
public interface IStepBuilderProcess<TInput>
{
    IStepBuilderWriteTo<TOutput> ProcessWith<TOutput>(IProcessor<TInput, TOutput> processor);
    IStepBuilderWriteTo<TOutput> ProcessWith<TOutput>(Func<TInput, TOutput> processor);
    IStepBuilderOptions WriteTo(IWriter<TInput> writer);
    IStepBuilderOptions WriteTo(Func<IEnumerable<TInput>, Task> writeAction);
}

/// <summary>
/// Third stage: provide a writer for the processed output.
/// </summary>
public interface IStepBuilderWriteTo<TOutput>
{
    IStepBuilderOptions WriteTo(IWriter<TOutput> writer);
    IStepBuilderOptions WriteTo(Func<IEnumerable<TOutput>, Task> writeAction);
}

/// <summary>
/// Marker interface for a fully-configured step.
/// Used as the return type of the lambda-based <c>AddStep</c> overload.
/// </summary>
public interface IStepBuilderFinal;

/// <summary>
/// Final stage: configure optional settings (skip policy, chunk size, listeners).
/// </summary>
public interface IStepBuilderOptions : IStepBuilderFinal
{
    IStepBuilderOptions WithSkipPolicy(SkipPolicy skipPolicy);
    IStepBuilderOptions WithListener(IStepListener listener);
    IStepBuilderOptions WithChunkSize(int chunkSize);
}

/// <summary>
/// Terminal stage for a tasklet step: attach a listener.
/// </summary>
public interface ITaskletStepBuilder : IStepBuilderFinal
{
    ITaskletStepBuilder WithListener(IStepListener listener);
}
