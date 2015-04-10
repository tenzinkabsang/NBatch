using System;

namespace NBatch.Main.Core
{
    public sealed class SkipContext
    {
        public long RowNumber { get; private set; }
        public string StepName { get; set; }
        public Exception Exception { get; private set; }

        public SkipContext(string stepName, long rowNumber, Exception ex)
        {
            StepName = stepName;
            RowNumber = rowNumber;
            Exception = ex;
        }

        public Type GetExceptionType()
        {
            return Exception.GetType();
        }
    }
}