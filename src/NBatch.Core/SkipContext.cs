using System;

namespace NBatch.Core
{
    public sealed class SkipContext
    {
        public int LineNumber { get; private set; }
        public string StepName { get; set; }
        public Exception Exception { get; private set; }

        public SkipContext(string stepName, int lineNumber, Exception ex)
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