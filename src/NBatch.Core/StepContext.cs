namespace NBatch.Core
{
    public sealed class StepContext
    {
        internal StepContext(string stepName)
        {
            StepName = stepName;
        }

        public string StepName { get; private set; }
        public long StepIndex { get; set; }
        public long HeaderIndexValue { get; set; }
        public int NumberOfItemsProcessed { get; set; }
        public bool Skip { get; set; }

        public bool ShouldSkip
        {
            get { return Skip || FirstIterationWithLinesToSkipAndChunkSizeOfEqualValue(); }
        }

        public bool ExceptionThrown { get; set; }

        private bool FirstIterationWithLinesToSkipAndChunkSizeOfEqualValue()
        {
            return StepIndex == HeaderIndexValue;
        }
    }
}