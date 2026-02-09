using NBatch.Core.Interfaces;

namespace NBatch.Core;

/// <summary>
/// First stage: a reader must be provided.
/// </summary>
public interface IStepBuilderReadFrom
{
    IStepBuilderWriteTo<TInput> ReadFrom<TInput>(IReader<TInput> reader);
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
    IStepBuilderOptions<TInput, TOutput> WithChunkSize(int chunkSize);
    IStepBuilderReadFrom AddStep(string stepName);
    Job Build();
}
