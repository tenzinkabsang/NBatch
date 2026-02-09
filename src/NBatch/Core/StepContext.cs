namespace NBatch.Core;

public sealed class StepContext
{
    public string StepName { get; set; } = string.Empty;
    public string JobName { get; set; } = string.Empty;
    public long StepIndex { get; set; }
    public int NumberOfItemsReceived { get; set; }
    public int NumberOfItemsProcessed { get; set; }
    public bool Skip { get; set; }
    public bool IsInitialRun { get; set; }
    public int ChunkSize { get; set; }
    public bool Error { get; set; }

    public long NextStepIndex => StepIndex + ChunkSize;

    public bool HasNext
    {
        get
        {
            if (IsInitialRun || Skip)
                return true;
            return NumberOfItemsReceived > 0 || StepIndex == ChunkSize;
        }
    }

    public StepContext() { /** Needed by ORM **/ }

    private StepContext(string stepName, long stepIndex, int numOfItemsProcessed, bool isInitialRun, int chunkSize)
    {
        StepName = stepName;
        StepIndex = stepIndex;
        NumberOfItemsProcessed = numOfItemsProcessed;
        IsInitialRun = isInitialRun;
        ChunkSize = chunkSize;
    }

    public static StepContext InitialRun(StepContext ctx, int chunkSize)
    {
        long index = RetryPreviousIfFailed(ctx, chunkSize);
        return new StepContext(ctx.StepName, index, ctx.NumberOfItemsProcessed, true, chunkSize);
    }

    public static StepContext Increment(StepContext ctx, int numberOfItemsReceived, int numberOfItemsProcessed, bool skipped)
    {
        return new StepContext(ctx.StepName, ctx.NextStepIndex, numberOfItemsProcessed, false, ctx.ChunkSize)
        {
            NumberOfItemsReceived = numberOfItemsReceived,
            Skip = skipped
        };
    }

    private static long RetryPreviousIfFailed(StepContext ctx, int chunkSize)
    {
        if (ctx.NumberOfItemsProcessed == 0 && ctx.StepIndex - chunkSize > 0)
            return ctx.StepIndex - chunkSize;

        return ctx.StepIndex;
    }
}