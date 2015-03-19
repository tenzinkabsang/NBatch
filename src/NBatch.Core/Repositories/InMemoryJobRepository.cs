using System;
using System.Collections.Generic;
using System.Linq;

namespace NBatch.Core.Repositories
{
    sealed class InMemoryJobRepository : IJobRepository
    {
        private readonly IList<int> _dbIndexes = new List<int> {0};
        private int _exceptionCount = 0;

        public int GetStartIndex(string stepName)
        {
            return _dbIndexes.Last();
        }

        public void SaveIndex(string stepName, int index)
        {
            _dbIndexes.Add(index);
        }

        public int GetExceptionCount(SkipContext context)
        {
            return _exceptionCount;
        }

        public void IncrementExceptionCount(SkipContext skipContext, int currentCount)
        {
            ++_exceptionCount;
            Console.WriteLine("Skippable exception on line: {0} - {1}", skipContext.LineNumber, skipContext.Exception.Message);
        }
    }
}