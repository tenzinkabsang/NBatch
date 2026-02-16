using System.Runtime.ExceptionServices;
using Microsoft.Extensions.Logging;
using NBatch.Core.Interfaces;
using NBatch.Core.Repositories;

namespace NBatch.Core;

/// <summary>
/// A chunk-oriented step that reads, optionally processes, and writes items.
/// Supports skip policies, and persists progress for restart-on-failure.
/// </summary>
internal class Step<TInput, TOutput> : IStep
{
    private readonly IReader<TInput> _reader;
    private readonly IProcessor<TInput, TOutput> _processor;
    private readonly IWriter<TOutput> _writer;
    private readonly IStepRepository _stepRepository;
    private readonly ILogger _logger;
    private readonly SkipPolicy _skipPolicy;

    public string Name { get; }
    public int ChunkSize { get; }

    internal Step(
        string stepName,
        IReader<TInput> reader,
        IProcessor<TInput, TOutput>? processor,
        IWriter<TOutput> writer,
        IStepRepository stepRepository,
        ILogger logger,
        SkipPolicy? skipPolicy = null,
        int chunkSize = 10)
    {
        Name = stepName;
        _reader = reader;
        _processor = processor ?? new DefaultProcessor<TInput, TOutput>();
        _writer = writer;
        _stepRepository = stepRepository;
        _logger = logger;
        _skipPolicy = skipPolicy ?? SkipPolicy.None;
        ChunkSize = chunkSize;
    }

    private enum ReadOutcome { Items, EndOfData, ReadError }
    private readonly record struct ChunkReadResult(ReadOutcome Outcome, List<TInput> Items, StepContext Context);

    /// <summary>
    /// Processes all chunks sequentially using the Reader, Processor and Writer.
    /// Tracks each chunk iteration via the repository to support restart on failure.
    /// </summary>
    public async Task<StepResult> ProcessAsync(CancellationToken cancellationToken = default)
    {
        var savedState = await _stepRepository.GetStartIndexAsync(Name, cancellationToken);
        var ctx = StepContext.InitialRun(savedState, ChunkSize);
        int totalRead = 0, totalProcessed = 0, totalSkipped = 0;

        if (ctx.StepIndex > 0)
            _logger.LogInformation("Step '{StepName}' resuming from index {StepIndex}", Name, ctx.StepIndex);

        try
        {
            while (ctx.HasNext)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var readResult = await ReadChunkAsync(ctx, cancellationToken);

                if (readResult.Outcome == ReadOutcome.EndOfData)
                    break;

                if (readResult.Outcome == ReadOutcome.ReadError)
                {
                    ctx = readResult.Context;
                }
                else
                {
                    long stepId = await _stepRepository.InsertStepAsync(ctx.StepName, ctx.NextStepIndex, cancellationToken);
                    ctx = await ProcessChunkAsync(ctx, stepId, readResult.Items, cancellationToken);
                }

                totalRead += ctx.NumberOfItemsReceived;
                totalProcessed += ctx.NumberOfItemsProcessed;

                if (ctx.Skip)
                    totalSkipped++;
            }
        }
        finally
        {
            await DisposeIfNeededAsync(_reader);
            await DisposeIfNeededAsync(_writer);
        }

        return new StepResult(Name, true, totalRead, totalProcessed, totalSkipped);
    }

    private static async ValueTask DisposeIfNeededAsync(object component)
    {
        if (component is IAsyncDisposable asyncDisposable)
            await asyncDisposable.DisposeAsync();
        else if (component is IDisposable disposable)
            disposable.Dispose();
    }

    /// <summary>
    /// Reads the next chunk from the reader. On failure, evaluates the skip policy
    /// and returns a <see cref="ReadOutcome.ReadError"/> result with the updated context.
    /// </summary>
    private async Task<ChunkReadResult> ReadChunkAsync(StepContext ctx, CancellationToken cancellationToken)
    {
        List<TInput> items;
        try
        {
            items = (await _reader.ReadAsync(ctx.StepIndex, ChunkSize, cancellationToken)).ToList();
        }
        catch (Exception ex)
        {
            long errorStepId = await _stepRepository.InsertStepAsync(ctx.StepName, ctx.NextStepIndex, cancellationToken);
            var errorCtx = await HandleErrorAsync(ctx, errorStepId, 0, ex, cancellationToken);
            return new ChunkReadResult(ReadOutcome.ReadError, [], errorCtx);
        }

        if (items.Count == 0)
            return new ChunkReadResult(ReadOutcome.EndOfData, [], ctx);

        return new ChunkReadResult(ReadOutcome.Items, items, ctx);
    }

    /// <summary>
    /// Processes and writes a single chunk of items.
    /// On failure, the skip policy determines whether to skip the chunk or propagate the error.
    /// </summary>
    private async Task<StepContext> ProcessChunkAsync(StepContext ctx, long stepId, List<TInput> items, CancellationToken cancellationToken)
    {
        int itemsRead = items.Count;

        try
        {
            List<TOutput> processedItems = [];
            foreach (var item in items)
            {
                var result = await _processor.ProcessAsync(item, cancellationToken);
                processedItems.Add(result);
            }

            if (processedItems.Count > 0)
                await _writer.WriteAsync(processedItems, cancellationToken);

            int itemsWritten = processedItems.Count;
            await _stepRepository.UpdateStepAsync(stepId, itemsWritten, error: false, skipped: false, cancellationToken);

            _logger.LogDebug("Step '{StepName}' chunk at index {Index} — read {Read}, wrote {Wrote}",
                Name, ctx.StepIndex, itemsRead, itemsWritten);

            return StepContext.Increment(ctx, itemsRead, itemsWritten, skipped: false);
        }
        catch (Exception ex)
        {
            return await HandleErrorAsync(ctx, stepId, itemsRead, ex, cancellationToken);
        }
    }

    /// <summary>
    /// Evaluates the skip policy for a failed chunk. If the error is skippable, logs a warning
    /// and advances to the next chunk. Otherwise logs a fatal error and rethrows.
    /// </summary>
    private async Task<StepContext> HandleErrorAsync(
        StepContext ctx, long stepId, int itemsRead, Exception ex, CancellationToken cancellationToken)
    {
        var skipContext = new SkipContext(ctx.StepName, ctx.NextStepIndex, ex);
        bool skipped = await _skipPolicy.IsSatisfiedByAsync(_stepRepository, skipContext, cancellationToken);

        await _stepRepository.UpdateStepAsync(stepId, 0, error: true, skipped, cancellationToken);

        if (skipped)
        {
            _logger.LogWarning(ex, "Step '{StepName}' chunk at index {Index} — skipped ({ExceptionType})",
                Name, ctx.StepIndex, ex.GetType().Name);
            return StepContext.Increment(ctx, itemsRead, 0, skipped: true);
        }

        _logger.LogError(ex, "Step '{StepName}' chunk at index {Index} — fatal error", Name, ctx.StepIndex);
        ExceptionDispatchInfo.Throw(ex);
        return default; // unreachable
    }
}