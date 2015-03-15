using System;

namespace NBatch.Core
{
    public sealed class SkipContext
    {
        public int LineNumber { get; private set; }
        public Exception Exception { get; private set; }

        public SkipContext(int lineNumber, Exception ex)
        {
            LineNumber = lineNumber;
            Exception = ex;
        }

        public Type GetExceptionType()
        {
            return Exception.GetType();
        }
    }
}