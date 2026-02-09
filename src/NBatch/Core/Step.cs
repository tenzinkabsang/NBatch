using NBatch.Core.Interfaces;
using NBatch.Core.Repositories;

namespace NBatch.Core;

/// <summary>
/// Step consists of three parts: Reader, (optional) Processor and a Writer.
/// It supports skipping certain types of `ERRORS` as well as the `NUMBER` of times errors should be skipped. 
/// </summary>
/// <typeparam name="TInput">Input data for the Reader.</typeparam>
/// <typeparam name="TOutput">Output data for the Processor & the Writer.</typeparam>
/// <param name="stepName">A unique name for the Step.</param>
/// <param name="reader">Used to read data from various sources (file, databases, etc,.) and feeds into the processor.</param>
/// <param name="processor">Handles any intermediary processes that needs to be performed on the data before sending it to a writer</param>
/// <param name="writer">Used to write/save the processed items.</param>
/// <param name="chunkSize">Ability to perform operation in chunks.</param>
internal class Step<TInput, TOutput>(string stepName,
    IReader<TInput> reader,
    IProcessor<TInput, TOutput>? processor,
    IWriter<TOutput> writer,
    SkipPolicy? skipPolicy = null,
    int chunkSize = 10) : IStep
{
    private readonly IProcessor<TInput, TOutput> _processor = processor ?? new DefaultProcessor<TInput, TOutput>();
    private readonly SkipPolicy _skipPolicy = skipPolicy ?? SkipPolicy.None;
    public string Name { get; init; } = stepName;
    public int ChunkSize { get; init; } = chunkSize;

    /// <summary>
    /// Processes all chunks sequentially using the Reader, Processor and Writer.
    /// Tracks each chunk iteration via the repository to support restart on failure.
    /// </summary>
    public async Task<StepResult> ProcessAsync(StepContext stepContext, IStepRepository stepRepository)
    {
        bool success = true;
        var ctx = StepContext.InitialRun(stepContext, ChunkSize);

        while (ctx.HasNext)
        {
            long stepId = await stepRepository.InsertStepAsync(ctx.StepName, ctx.NextStepIndex);
            (bool chunkSuccess, ctx) = await ProcessChunkAsync(ctx, stepId, stepRepository);
            success &= chunkSuccess;
        }

        return new StepResult(Name, success);
    }

    /// <summary>
    /// Reads, processes, and writes a single chunk. On a skippable error the chunk is
    /// marked as skipped and the next context is advanced. On a fatal error the step
    /// state is persisted before the exception propagates (context is not advanced).
    /// </summary>
    private async Task<(bool Success, StepContext NextContext)> ProcessChunkAsync(
        StepContext ctx, long stepId, IStepRepository stepRepository)
    {
        List<TInput> items = [];
        List<TOutput> processedItems = [];
        bool error = false, skip = false;

        try
        {
            items = (await reader.ReadAsync(ctx.StepIndex, ChunkSize)).ToList();

            foreach (var item in items)
            {
                var result = await _processor.ProcessAsync(item);
                processedItems.Add(result);
            }

            bool writeSuccess = await writer.WriteAsync(processedItems);

            var stepContext = StepContext.Increment(ctx, items.Count, processedItems.Count, false);

            return (writeSuccess, stepContext);
        }
        catch (Exception ex)
        {
            error = true;
            skip = await _skipPolicy.IsSatisfiedByAsync(stepRepository, new SkipContext(ctx.StepName, ctx.NextStepIndex, ex));

            if (!skip)
                throw;

            return (true, StepContext.Increment(ctx, items.Count, processedItems.Count, true));
        }
        finally
        {
            await stepRepository.UpdateStepAsync(stepId, processedItems.Count, error, skip);
        }
    }
}
