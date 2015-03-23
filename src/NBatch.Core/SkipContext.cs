using System;

namespace NBatch.Core
{
    public sealed class SkipContext
    {
        public long LineNumber { get; private set; }
        public string StepName { get; set; }
        public Exception Exception { get; private set; }

        public SkipContext(string stepName, long lineNumber, Exception ex)
        {
            StepName = stepName;
            LineNumber = lineNumber;
            Exception = ex;
        }

        public Type GetExceptionType()
        {
            return Exception.GetType();
        }
    }
}