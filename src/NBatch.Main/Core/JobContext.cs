namespace NBatch.Main.Core
{
    sealed class JobContext
    {
        public string JobName { get; private set; }
        public long StartIndex { get; private set; }

        public JobContext(string jobName, int startIndex)
        {
            JobName = jobName;
            StartIndex = startIndex;
        }
    }
}