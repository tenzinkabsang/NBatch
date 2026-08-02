using System.Runtime.ExceptionServices;
using Microsoft.Extensions.Logging;
using NBatch.Core.Interfaces;
using NBatch.Core.Repositories;

namespace NBatch.Core;

/// <summary>
/// A chunk-oriented step that reads, optionally processes, and writes items.
/// Supports skip policies, and persists progress for restart-on-failure.
/// <para>
/// Error handling is item-granular: when a chunk fails, the step falls back to
/// handling that chunk one item at a time so only the genuinely failing items
/// are skipped — the remaining items are still processed and written. The
/// processor is re-invoked for items of a failed chunk, so processors should be
/// idempotent.
/// </para>
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

    private enum ReadOutcome { Items, EndOfData, Scanned }
    private readonly record struct ChunkReadResult(ReadOutcome Outcome, List<TInput> Items, StepContext Context);
    private readonly record struct ScanResult(int ItemsRead, int ItemsWritten, int ItemsSkipped);

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

                if (readResult.Outcome == ReadOutcome.Scanned)
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
                totalSkipped += ctx.NumberOfItemsSkipped;
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
    /// Reads the next chunk from the reader. On failure, if the skip policy could
    /// apply, the chunk range is re-read one item at a time so only the failing
    /// positions are skipped; otherwise the error is recorded and rethrown.
    /// </summary>
    private async Task<ChunkReadResult> ReadChunkAsync(StepContext ctx, CancellationToken cancellationToken)
    {
        List<TInput> items;
        try
        {
            items = (await _reader.ReadAsync(ctx.StepIndex, ChunkSize, cancellationToken)).ToList();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            long stepId = await _stepRepository.InsertStepAsync(ctx.StepName, ctx.NextStepIndex, CancellationToken.None);

            if (!_skipPolicy.Matches(ex))
                await RecordFatalAndThrowAsync(ctx, stepId, ex);

            _logger.LogWarning(ex, "Step '{StepName}' chunk read at index {Index} failed — scanning items individually",
                Name, ctx.StepIndex);

            var scan = await ScanReadFailuresAsync(ctx, stepId, cancellationToken);
            return new ChunkReadResult(ReadOutcome.Scanned, [],
                StepContext.Increment(ctx, scan.ItemsRead, scan.ItemsWritten, scan.ItemsSkipped));
        }

        if (items.Count == 0)
            return new ChunkReadResult(ReadOutcome.EndOfData, [], ctx);

        return new ChunkReadResult(ReadOutcome.Items, items, ctx);
    }

    /// <summary>
    /// Processes and writes a single chunk of items. On failure, if the skip policy
    /// could apply, the already-read items are re-handled one at a time (processor
    /// re-invoked, single-item writes) so only failing items are skipped.
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

            // Commit progress with a non-cancellable token: the writer has already
            // persisted the data, so we must record the advance to prevent duplicate
            // processing on restart.
            await _stepRepository.UpdateStepAsync(stepId, itemsWritten, error: false, skipped: false, CancellationToken.None);

            _logger.LogDebug("Step '{StepName}' chunk at index {Index} — read {Read}, wrote {Wrote}",
                Name, ctx.StepIndex, itemsRead, itemsWritten);

            return StepContext.Increment(ctx, itemsRead, itemsWritten, itemsSkipped: 0);
        }
        catch (OperationCanceledException)
        {
            // Record the aborted chunk as an error so the restart backs up and
            // re-processes it — otherwise the pre-inserted row would look complete
            // and the chunk's items would be silently lost.
            await _stepRepository.UpdateStepAsync(stepId, 0, error: true, skipped: false, CancellationToken.None);
            throw;
        }
        catch (Exception ex)
        {
            if (!_skipPolicy.Matches(ex))
                await RecordFatalAndThrowAsync(ctx, stepId, ex);

            _logger.LogWarning(ex,
                "Step '{StepName}' chunk at index {Index} failed — scanning items individually (processor re-invoked)",
                Name, ctx.StepIndex);

            var scan = await ScanProcessWriteFailuresAsync(ctx, stepId, items, cancellationToken);
            return StepContext.Increment(ctx, itemsRead, scan.ItemsWritten, scan.ItemsSkipped);
        }
    }

    /// <summary>
    /// Item-at-a-time fallback after a chunk <em>read</em> failure: re-reads each
    /// position in the chunk range individually. Unreadable positions consult the
    /// skip policy; readable items are processed and written one at a time.
    /// </summary>
    private async Task<ScanResult> ScanReadFailuresAsync(StepContext ctx, long stepId, CancellationToken cancellationToken)
    {
        int read = 0, written = 0, skipped = 0;

        try
        {
            for (int i = 0; i < ChunkSize; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                long itemIndex = ctx.StepIndex + i;

                List<TInput> single;
                try
                {
                    single = (await _reader.ReadAsync(itemIndex, 1, cancellationToken)).ToList();
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    await SkipOrRethrowAsync(ctx, itemIndex, ex);
                    skipped++;
                    continue;
                }

                if (single.Count == 0)
                    break; // end of data mid-scan — partial chunk

                read++;
                if (await TryProcessAndWriteSingleAsync(single[0], ctx, itemIndex, cancellationToken))
                    written++;
                else
                    skipped++;
            }
        }
        catch (Exception)
        {
            // Fatal (non-skippable / limit exhausted) or cancelled mid-scan: record the
            // partial progress as an error so restart backs up and re-runs this chunk.
            // Items already written stay written (at-least-once semantics).
            await _stepRepository.UpdateStepAsync(stepId, written, error: true, skipped: skipped > 0, CancellationToken.None);
            throw;
        }

        await _stepRepository.UpdateStepAsync(stepId, written, error: false, skipped: skipped > 0, CancellationToken.None);
        return new ScanResult(read, written, skipped);
    }

    /// <summary>
    /// Item-at-a-time fallback after a chunk <em>process/write</em> failure:
    /// re-processes the already-read items individually.
    /// </summary>
    private async Task<ScanResult> ScanProcessWriteFailuresAsync(StepContext ctx, long stepId, List<TInput> items, CancellationToken cancellationToken)
    {
        int written = 0, skipped = 0;

        try
        {
            for (int i = 0; i < items.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                long itemIndex = ctx.StepIndex + i;

                if (await TryProcessAndWriteSingleAsync(items[i], ctx, itemIndex, cancellationToken))
                    written++;
                else
                    skipped++;
            }
        }
        catch (Exception)
        {
            await _stepRepository.UpdateStepAsync(stepId, written, error: true, skipped: skipped > 0, CancellationToken.None);
            throw;
        }

        await _stepRepository.UpdateStepAsync(stepId, written, error: false, skipped: skipped > 0, CancellationToken.None);
        return new ScanResult(items.Count, written, skipped);
    }

    /// <summary>
    /// Processes and writes one item. Returns true when written, false when the
    /// skip policy discarded it; throws when the error is not skippable or the
    /// skip limit is exhausted.
    /// </summary>
    private async Task<bool> TryProcessAndWriteSingleAsync(TInput item, StepContext ctx, long itemIndex, CancellationToken cancellationToken)
    {
        try
        {
            var output = await _processor.ProcessAsync(item, cancellationToken);
            await _writer.WriteAsync([output], cancellationToken);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await SkipOrRethrowAsync(ctx, itemIndex, ex);
            return false;
        }
    }

    /// <summary>
    /// Consults the skip policy for a single failed item. Returns normally when the
    /// item was skipped (the exception is recorded in the job store); rethrows when
    /// the error is not skippable or the skip limit is exhausted.
    /// </summary>
    private async Task SkipOrRethrowAsync(StepContext ctx, long itemIndex, Exception ex)
    {
        // Repository calls use CancellationToken.None: error bookkeeping must not
        // be defeated by a concurrently cancelled token.
        var skipContext = new SkipContext(ctx.StepName, itemIndex, ex);
        bool skippable = await _skipPolicy.IsSatisfiedByAsync(_stepRepository, skipContext, CancellationToken.None);

        if (!skippable)
        {
            _logger.LogError(ex, "Step '{StepName}' item at index {Index} — fatal error", Name, itemIndex);
            ExceptionDispatchInfo.Throw(ex);
        }

        _logger.LogWarning(ex, "Step '{StepName}' item at index {Index} — skipped ({ExceptionType})",
            Name, itemIndex, ex.GetType().Name);
    }

    /// <summary>Records a non-skippable chunk failure and rethrows.</summary>
    private async Task RecordFatalAndThrowAsync(StepContext ctx, long stepId, Exception ex)
    {
        await _stepRepository.UpdateStepAsync(stepId, 0, error: true, skipped: false, CancellationToken.None);
        _logger.LogError(ex, "Step '{StepName}' chunk at index {Index} — fatal error", Name, ctx.StepIndex);
        ExceptionDispatchInfo.Throw(ex);
    }
}
