using System.Collections.Generic;
using System.Linq;
using NBatch.Core.Repositories;

namespace NBatch.Core
{
    public sealed class Job
    {
        private readonly IJobRepository _jobRepository;
        private readonly IList<IStep> _steps; 

        public Job(IJobRepository jobRepository)
        {
            _jobRepository = jobRepository;
            _steps = new List<IStep>();
        }

        public Job AddStep(IStep step)
        {
            _steps.Add(step);
            return this;
        }

        public bool Start()
        {
            // Aggregate: same as reduce(initialValue, (first, second) -> function)
            bool success = _steps.Aggregate(true, (current, step) => current & step.Process(_jobRepository));
            return success;
        }
    }
}
