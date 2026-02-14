using NBatch.Core.Interfaces;

namespace NBatch.Core;

/// <summary>
/// First stage: provide a reader for chunk-oriented processing, or a tasklet for single-unit work.
/// </summary>
public interface IStepBuilderReadFrom
{
    /// <summary>Configures a reader for chunk-oriented processing.</summary>
    IStepBuilderProcess<TInput> ReadFrom<TInput>(IReader<TInput> reader);
    /// <summary>Configures a tasklet for single-unit work.</summary>
    ITaskletStepBuilder Execute(ITasklet tasklet);
    /// <inheritdoc cref="Execute(ITasklet)" />
    ITaskletStepBuilder Execute(Func<Task> action);
    /// <inheritdoc cref="Execute(ITasklet)" />
    ITaskletStepBuilder Execute(Func<CancellationToken, Task> action);
}

/// <summary>
/// Second stage: optionally process the items, or skip straight to writing.
/// </summary>
public interface IStepBuilderProcess<TInput>
{
    /// <summary>Adds a processor that transforms each item.</summary>
    IStepBuilderWriteTo<TOutput> ProcessWith<TOutput>(IProcessor<TInput, TOutput> processor);
    /// <inheritdoc cref="ProcessWith{TOutput}(IProcessor{TInput, TOutput})" />
    IStepBuilderWriteTo<TOutput> ProcessWith<TOutput>(Func<TInput, TOutput> processor);
    /// <summary>Writes items directly without processing.</summary>
    IStepBuilderOptions WriteTo(IWriter<TInput> writer);
    /// <inheritdoc cref="WriteTo(IWriter{TInput})" />
    IStepBuilderOptions WriteTo(Func<IEnumerable<TInput>, Task> writeAction);
}

/// <summary>
/// Third stage: provide a writer for the processed output.
/// </summary>
public interface IStepBuilderWriteTo<TOutput>
{
    /// <summary>Configures the writer for processed output.</summary>
    IStepBuilderOptions WriteTo(IWriter<TOutput> writer);
    /// <inheritdoc cref="WriteTo(IWriter{TOutput})" />
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
    /// <summary>Sets the skip policy for this step.</summary>
    IStepBuilderOptions WithSkipPolicy(SkipPolicy skipPolicy);
    /// <summary>Registers a step-level listener.</summary>
    IStepBuilderOptions WithListener(IStepListener listener);
    /// <summary>Sets the number of items to read per chunk. Default is 10.</summary>
    IStepBuilderOptions WithChunkSize(int chunkSize);
}

/// <summary>
/// Terminal stage for a tasklet step: attach a listener.
/// </summary>
public interface ITaskletStepBuilder : IStepBuilderFinal
{
    /// <summary>Registers a step-level listener for the tasklet.</summary>
    ITaskletStepBuilder WithListener(IStepListener listener);
}
