using Microsoft.Extensions.Logging;
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
    IStepRepository stepRepository,
    ILogger logger,
    SkipPolicy? skipPolicy = null,
    RetryPolicy? retryPolicy = null,
    int chunkSize = 10) : IStep
{
    private readonly IProcessor<TInput, TOutput> _processor = processor ?? new DefaultProcessor<TInput, TOutput>();
    private readonly SkipPolicy _skipPolicy = skipPolicy ?? SkipPolicy.None;
    private readonly RetryPolicy _retryPolicy = retryPolicy ?? RetryPolicy.None;
    public string Name { get; init; } = stepName;
    public int ChunkSize { get; init; } = chunkSize;

    /// <summary>
    /// Processes all chunks sequentially using the Reader, Processor and Writer.
    /// Tracks each chunk iteration via the repository to support restart on failure.
    /// </summary>
    public async Task<StepResult> ProcessAsync(CancellationToken cancellationToken = default)
    {
        var stepContext = await stepRepository.GetStartIndexAsync(Name, cancellationToken);
        int totalItemsRead = 0, totalItemsProcessed = 0, totalErrorsSkipped = 0;
        var ctx = StepContext.InitialRun(stepContext, ChunkSize);

        if (ctx.StepIndex > 0)
            logger.LogInformation("Step '{StepName}' resuming from index {StepIndex}", Name, ctx.StepIndex);

        while (ctx.HasNext)
        {
            cancellationToken.ThrowIfCancellationRequested();
            long stepId = await stepRepository.InsertStepAsync(ctx.StepName, ctx.NextStepIndex, cancellationToken);
            ctx = await ProcessChunkAsync(ctx, stepId, cancellationToken);
            totalItemsRead += ctx.NumberOfItemsReceived;
            totalItemsProcessed += ctx.NumberOfItemsProcessed;
            if (ctx.Skip) totalErrorsSkipped++;
        }

        return new StepResult(Name, true, totalItemsRead, totalItemsProcessed, totalErrorsSkipped);
    }

    /// <summary>
    /// Reads, processes, and writes a single chunk with retry support.
    /// Retryable exceptions are re-attempted up to the configured limit before
    /// falling through to the skip policy. On a fatal error the step state is
    /// persisted before the exception propagates.
    /// </summary>
    private async Task<StepContext> ProcessChunkAsync(
        StepContext ctx, long stepId, CancellationToken cancellationToken)
    {
        List<TInput> items = [];
        List<TOutput> processedItems = [];
        bool error = false, skip = false;

        try
        {
            int attempt = 0;
            while (true)
            {
                try
                {
                    attempt++;
                    items = (await reader.ReadAsync(ctx.StepIndex, ChunkSize, cancellationToken)).ToList();

                    processedItems = [];
                    foreach (var item in items)
                    {
                        var result = await _processor.ProcessAsync(item, cancellationToken);
                        processedItems.Add(result);
                    }

                    await writer.WriteAsync(processedItems, cancellationToken);

                    logger.LogDebug("Step '{StepName}' chunk at index {Index} — read {Read}, wrote {Wrote}",
                        Name, ctx.StepIndex, items.Count, processedItems.Count);

                    return StepContext.Increment(ctx, items.Count, processedItems.Count, false);
                }
                catch (Exception ex) when (_retryPolicy.ShouldRetry(ex, attempt))
                {
                    logger.LogWarning(ex, "Step '{StepName}' chunk at index {Index} — retry attempt {Attempt}",
                        Name, ctx.StepIndex, attempt);
                    await _retryPolicy.WaitAsync(cancellationToken);
                }
            }
        }
        catch (Exception ex)
        {
            error = true;
            skip = await _skipPolicy.IsSatisfiedByAsync(stepRepository, new SkipContext(ctx.StepName, ctx.NextStepIndex, ex), cancellationToken);

            if (skip)
            {
                logger.LogWarning(ex, "Step '{StepName}' chunk at index {Index} — skipped ({ExceptionType})",
                    Name, ctx.StepIndex, ex.GetType().Name);
                return StepContext.Increment(ctx, items.Count, processedItems.Count, true);
            }

            logger.LogError(ex, "Step '{StepName}' chunk at index {Index} — fatal error", Name, ctx.StepIndex);
            throw;
        }
        finally
        {
            int itemsCompleted = (error && !skip) ? 0 : processedItems.Count;
            await stepRepository.UpdateStepAsync(stepId, itemsCompleted, error, skip, cancellationToken);
        }
    }
}
