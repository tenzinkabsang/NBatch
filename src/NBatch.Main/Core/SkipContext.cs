using System;

namespace NBatch.Main.Core
{
    public sealed class SkipContext
    {
        public long StepIndex { get; private set; }
        public string StepName { get; set; }
        public Exception Exception { get; private set; }

        public SkipContext(string stepName, long stepIndex, Exception ex)
        {
            StepName = stepName;
            StepIndex = stepIndex;
            Exception = ex;
        }

        public Type GetExceptionType()
        {
            return Exception.GetType();
        }
    }
}