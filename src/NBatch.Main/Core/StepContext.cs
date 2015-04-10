namespace NBatch.Main.Core
{
    public sealed class StepContext
    {
        private long _linesToSkip;
        public string StepName { get; private set; }
        public long StepIndex { get; private set; }
        public int NumberOfItemsProcessed { get; private set; }
        public bool Skip { get; private set; }
        public bool IsInitialRun { get; private set; }
        public int ChunkSize { get; private set; }
        public int NumberOfItemsReceived { get; private set; }

        public long RowNumber
        {
            get { return StepIndex + _linesToSkip; }
        }

        private StepContext(string stepName, bool isInitialRun, long startIndex, int chunkSize)
        {
            StepName = stepName;
            IsInitialRun = isInitialRun;
            StepIndex = startIndex;
            ChunkSize = chunkSize;
        }

        public bool HasNext
        {
            get
            {
                if (IsInitialRun || Skip)
                    return true;
                return NumberOfItemsReceived > 0 || StepIndex == ChunkSize;
            }
        }

        public static StepContext InitialRun(string stepName, long startIndex, int chunkSize)
        {
            return new StepContext(stepName, true, startIndex, chunkSize);
        }

        public static StepContext Increment(StepContext ctx, int numberOfItemsReceived, int numberOfItemsProcessed, bool skipped)
        {
            long nextIndex = ctx.StepIndex + ctx.ChunkSize;
            long linesToSkipValue = CalculateLinesToSkipValue(nextIndex, numberOfItemsProcessed, ctx.ChunkSize, ctx._linesToSkip);
            return new StepContext(ctx.StepName, false, nextIndex, ctx.ChunkSize)
                   {
                       NumberOfItemsReceived = numberOfItemsReceived,
                       NumberOfItemsProcessed = numberOfItemsProcessed,
                       Skip = skipped,
                       _linesToSkip = linesToSkipValue
                   };
        }

        private static long CalculateLinesToSkipValue(long stepIndex, int numOfItemsProcessed, int chunkSize, long linesToSkip)
        {
            const int FIRST_ITERATION = 1;
            if (stepIndex == FIRST_ITERATION)
                return chunkSize - numOfItemsProcessed;

            return linesToSkip;
        }
    }
}