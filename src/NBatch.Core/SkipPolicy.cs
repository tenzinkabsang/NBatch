using System;
using System.Linq;
using NBatch.Core.Repositories;

namespace NBatch.Core
{
    public sealed class SkipPolicy
    {
        public int SkipLimit { get; set; }
        private Type[] _skippableExceptions;

        public bool IsSatisfiedBy(IJobRepository jobRepository, SkipContext skipContext)
        {
            if (SkipLimit == 0)
                return false;

            int exceptionCount = jobRepository.GetExceptionCount();
            if (exceptionCount > SkipLimit || !_skippableExceptions.Contains(skipContext.GetExceptionType()))
                return false;

            jobRepository.IncrementExceptionCount();
            jobRepository.SaveExceptionDetails(skipContext);
            return true;
        }

        public void AddSkippableExceptions(Type[] exceptions)
        {
            if(!exceptions.All(ex => ex.IsSubclassOf(typeof(Exception)) || ex == typeof(Exception)))
                throw new Exception("Invalid Skippable Exception Type");

            _skippableExceptions = exceptions;
        }
    }
}