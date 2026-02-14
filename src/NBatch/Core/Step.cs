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

        while (ctx.HasNext)
        {
            cancellationToken.ThrowIfCancellationRequested();

            long stepId = await _stepRepository.InsertStepAsync(ctx.StepName, ctx.NextStepIndex, cancellationToken);
            ctx = await ProcessChunkAsync(ctx, stepId, cancellationToken);

            totalRead += ctx.NumberOfItemsReceived;
            totalProcessed += ctx.NumberOfItemsProcessed;
            if (ctx.Skip) totalSkipped++;
        }

        return new StepResult(Name, true, totalRead, totalProcessed, totalSkipped);
    }

    /// <summary>
    /// Reads, processes, and writes a single chunk.
    /// On failure, the skip policy determines whether to skip the chunk or propagate the error.
    /// Step state is always persisted in the finally block.
    /// </summary>
    private async Task<StepContext> ProcessChunkAsync(StepContext ctx, long stepId, CancellationToken cancellationToken)
    {
        int itemsRead = 0;
        int itemsWritten = 0;
        bool error = false;
        bool skipped = false;

        try
        {
            var items = (await _reader.ReadAsync(ctx.StepIndex, ChunkSize, cancellationToken)).ToList();
            itemsRead = items.Count;

            List<TOutput> processedItems = [];
            foreach (var item in items)
            {
                var result = await _processor.ProcessAsync(item, cancellationToken);
                processedItems.Add(result);
            }

            await _writer.WriteAsync(processedItems, cancellationToken);
            itemsWritten = processedItems.Count;

            _logger.LogDebug("Step '{StepName}' chunk at index {Index} — read {Read}, wrote {Wrote}",
                Name, ctx.StepIndex, itemsRead, itemsWritten);

            return StepContext.Increment(ctx, itemsRead, itemsWritten, skipped: false);
        }
        catch (Exception ex)
        {
            error = true;

            var skipContext = new SkipContext(ctx.StepName, ctx.NextStepIndex, ex);
            skipped = await _skipPolicy.IsSatisfiedByAsync(_stepRepository, skipContext, cancellationToken);

            if (skipped)
            {
                _logger.LogWarning(ex, "Step '{StepName}' chunk at index {Index} — skipped ({ExceptionType})",
                    Name, ctx.StepIndex, ex.GetType().Name);
                return StepContext.Increment(ctx, itemsRead, itemsWritten, skipped: true);
            }

            _logger.LogError(ex, "Step '{StepName}' chunk at index {Index} — fatal error", Name, ctx.StepIndex);
            throw;
        }
        finally
        {
            int completedItems = (error && !skipped) ? 0 : itemsWritten;
            await _stepRepository.UpdateStepAsync(stepId, completedItems, error, skipped, cancellationToken);
        }
    }
}
