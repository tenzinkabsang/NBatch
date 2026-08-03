namespace NBatch.Core;

/// <summary>
/// Tracks the current position and state of a step's chunk-processing loop.
/// Created via factory methods — not intended for direct construction outside of repositories.
/// </summary>
internal sealed class StepContext
{
    public string StepName { get; set; } = string.Empty;
    public string JobName { get; set; } = string.Empty;
    public long StepIndex { get; set; }
    public int NumberOfItemsReceived { get; set; }
    public int NumberOfItemsProcessed { get; set; }
    public int NumberOfItemsSkipped { get; set; }
    public int ChunkSize { get; set; }
    public bool Skip { get; set; }

    /// <summary>Whether the last chunk ended with an unrecoverable error.</summary>
    public bool Error { get; set; }

    /// <summary>Whether this is the first iteration of the processing loop.</summary>
    public bool FirstIteration { get; set; }

    public long NextStepIndex => StepIndex + ChunkSize;

    public bool HasNext => FirstIteration || Skip || NumberOfItemsReceived > 0;

    /// <summary>Parameterless constructor required by repository hydration.</summary>
    public StepContext() { }

    /// <summary>
    /// Creates the initial context for a step run. The repository has already
    /// resolved <see cref="StepIndex"/> to the last committed position (a failed
    /// or interrupted chunk resumes at its own start index), so no back-up
    /// arithmetic happens here.
    /// </summary>
    public static StepContext InitialRun(StepContext previous, int chunkSize)
    {
        return new StepContext
        {
            StepName = previous.StepName,
            StepIndex = previous.StepIndex,
            NumberOfItemsProcessed = previous.NumberOfItemsProcessed,
            ChunkSize = chunkSize,
            FirstIteration = true
        };
    }

    /// <summary>
    /// Advances the context to the next chunk after an iteration.
    /// <paramref name="itemsSkipped"/> is the number of individual items the skip
    /// policy discarded during the iteration (0 for a fully successful chunk).
    /// </summary>
    public static StepContext Increment(StepContext current, int itemsReceived, int itemsProcessed, int itemsSkipped)
    {
        return new StepContext
        {
            StepName = current.StepName,
            StepIndex = current.NextStepIndex,
            NumberOfItemsReceived = itemsReceived,
            NumberOfItemsProcessed = itemsProcessed,
            NumberOfItemsSkipped = itemsSkipped,
            ChunkSize = current.ChunkSize,
            Skip = itemsSkipped > 0
        };
    }

}