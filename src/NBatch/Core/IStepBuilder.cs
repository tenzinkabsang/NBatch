using NBatch.Core.Interfaces;

namespace NBatch.Core;

/// <summary>
/// First stage: provide a reader for chunk-oriented processing, or a tasklet for single-unit work.
/// </summary>
public interface IStepBuilderReadFrom
{
    /// <summary>Configures a reader for chunk-oriented processing.</summary>
    IStepBuilderProcess<TInput> ReadFrom<TInput>(IReader<TInput> reader);
    /// <summary>
    /// Configures a reader resolved from the service provider, fresh for every run.
    /// Available only for jobs registered via <c>services.AddNBatch(...)</c>.
    /// </summary>
    /// <typeparam name="TReader">The reader type to resolve or construct.</typeparam>
    /// <typeparam name="TItem">The item type the reader produces.</typeparam>
    IStepBuilderProcess<TItem> ReadFrom<TReader, TItem>() where TReader : class, IReader<TItem>;
    /// <summary>Configures a tasklet for single-unit work.</summary>
    ITaskletStepBuilder Execute(ITasklet tasklet);
    /// <summary>
    /// Configures a tasklet resolved from the service provider, fresh for every run.
    /// Available only for jobs registered via <c>services.AddNBatch(...)</c>.
    /// </summary>
    ITaskletStepBuilder Execute<TTasklet>() where TTasklet : class, ITasklet;
    /// <inheritdoc cref="Execute(ITasklet)" />
    ITaskletStepBuilder Execute(Func<Task> action);
    /// <inheritdoc cref="Execute(ITasklet)" />
    ITaskletStepBuilder Execute(Func<CancellationToken, Task> action);
    /// <inheritdoc cref="Execute(ITasklet)" />
    ITaskletStepBuilder Execute(Action action);
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
    /// <inheritdoc cref="ProcessWith{TOutput}(IProcessor{TInput, TOutput})" />
    IStepBuilderWriteTo<TOutput> ProcessWith<TOutput>(Func<TInput, CancellationToken, Task<TOutput>> processor);
    /// <summary>
    /// Adds a processor resolved from the service provider, fresh for every run.
    /// Available only for jobs registered via <c>services.AddNBatch(...)</c>.
    /// </summary>
    /// <typeparam name="TProcessor">The processor type to resolve or construct.</typeparam>
    /// <typeparam name="TOutput">The processor's output item type.</typeparam>
    IStepBuilderWriteTo<TOutput> ProcessWith<TProcessor, TOutput>() where TProcessor : class, IProcessor<TInput, TOutput>;
    /// <summary>Writes items directly without processing.</summary>
    IStepBuilderOptions WriteTo(IWriter<TInput> writer);
    /// <inheritdoc cref="WriteTo(IWriter{TInput})" />
    IStepBuilderOptions WriteTo(Func<IEnumerable<TInput>, Task> writeAction);
    /// <inheritdoc cref="WriteTo(IWriter{TInput})" />
    IStepBuilderOptions WriteTo(Func<IEnumerable<TInput>, CancellationToken, Task> writeAction);
    /// <summary>
    /// Configures a writer resolved from the service provider, fresh for every run.
    /// Available only for jobs registered via <c>services.AddNBatch(...)</c>.
    /// </summary>
    IStepBuilderOptions WriteTo<TWriter>() where TWriter : class, IWriter<TInput>;
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
    /// <inheritdoc cref="WriteTo(IWriter{TOutput})" />
    IStepBuilderOptions WriteTo(Func<IEnumerable<TOutput>, CancellationToken, Task> writeAction);
    /// <summary>
    /// Configures a writer resolved from the service provider, fresh for every run.
    /// Available only for jobs registered via <c>services.AddNBatch(...)</c>.
    /// </summary>
    IStepBuilderOptions WriteTo<TWriter>() where TWriter : class, IWriter<TOutput>;
}

/// <summary>
/// Marker interface for a fully-configured step.
/// Used as the return type of the lambda-based <c>AddStep</c> overload.
/// </summary>
public interface IStepBuilderFinal;

/// <summary>
/// Final stage: configure optional settings (skip policy, retry policy, chunk size, listeners).
/// </summary>
public interface IStepBuilderOptions : IStepBuilderFinal
{
    /// <summary>Sets the skip policy for this step.</summary>
    IStepBuilderOptions WithSkipPolicy(SkipPolicy skipPolicy);
    /// <summary>
    /// Sets the retry policy for transient failures. Retries happen before the
    /// skip policy is consulted.
    /// </summary>
    IStepBuilderOptions WithRetryPolicy(RetryPolicy retryPolicy);
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
