namespace NBatch.Main.Core
{
    public sealed class StepContext
    {
        public string StepName { get; private set; }
        public long StepIndex { get; private set; }
        public int NumberOfItemsProcessed { get; private set; }
        public bool Skip { get; private set; }
        public bool IsInitialRun { get; private set; }
        public int ChunkSize { get; private set; }
        public int NumberOfItemsReceived { get; private set; }

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
            return new StepContext(ctx.StepName, false, nextIndex, ctx.ChunkSize)
                   {
                       NumberOfItemsReceived = numberOfItemsReceived,
                       NumberOfItemsProcessed = numberOfItemsProcessed,
                       Skip = skipped
                   };
        }
    }
}