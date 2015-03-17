using System;
using System.Collections.Generic;
using System.Linq;

namespace NBatch.Core.Repositories
{
    sealed class JobRepository : IJobRepository
    {
        private readonly IList<int> _dbIndexes = new List<int> {0};
        private int _exceptionCount = 0;

        public int GetStartIndex()
        {
            return _dbIndexes.Last();
        }

        public void SaveIndex(int index)
        {
            _dbIndexes.Add(index);
        }

        public int GetExceptionCount()
        {
            return _exceptionCount;
        }

        public void IncrementExceptionCount()
        {
            ++_exceptionCount;
        }

        public void SaveExceptionDetails(SkipContext skipContext)
        {
            Console.WriteLine("Skippable exception on line: {0} - {1}", skipContext.LineNumber, skipContext.Exception.Message);
        }
    }
}