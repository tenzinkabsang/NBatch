using System;
using System.Collections.Generic;
using System.Linq;

namespace NBatch.Main.Core.Repositories
{
    sealed class InMemoryJobRepository : IJobRepository
    {
        private readonly IList<long> _dbIndexes = new List<long> {0};
        private int _exceptionCount = 0;


        public StepContext GetStartIndex(string stepName)
        {
            throw new NotImplementedException();
        }

        public void CreateJobRecord(ICollection<string> stepNames)
        {
            
        }

        public long InsertStep(string stepName, long stepIndex)
        {
            return 0;
        }

        public long UpdateStep(long stepId, int numberOfItemsProcessed, bool error, bool skipped)
        {
            return 0;
        }
        
        public void SaveStepContext(StepContext stepContext)
        {
            _dbIndexes.Add(stepContext.StepIndex);
        }

        public int GetExceptionCount(SkipContext context)
        {
            return _exceptionCount;
        }

        public void SaveExceptionInfo(SkipContext skipContext, int currentCount)
        {
            ++_exceptionCount;
            Console.WriteLine("Skippable exception on line: {0} - {1}", skipContext.StepIndex, skipContext.Exception.Message);
        }
    }
}