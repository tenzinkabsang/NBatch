using NBatch.Common;
using NBatch.Core.Repositories;
using System.Collections.Generic;
using System.Linq;

namespace NBatch.Core
{
    public sealed class Job
    {
        private static readonly ILogger Log = LogManager.GetLogger<Job>();
        private readonly IJobRepository _repo;
        private readonly IDictionary<string, IStep> _steps;

        public Job()
            : this(new InMemoryJobRepository())
        {
        }

        public Job(string jobName, string conn)
            :this(new SqlJobRepository(jobName, conn))
        {
            
        }

        internal Job(IJobRepository repo)
        {
            _repo = repo;
            _steps = new Dictionary<string, IStep>();
        }

        public Job AddStep(IStep newStep)
        {
            Ensure.UniqueStepName(_steps.Keys, newStep);
            _steps.Add(newStep.Name, newStep);
            return this;
        }

        public bool Start()
        {
            bool success = _steps.Values.Aggregate(true, (current, step) =>
                                                         {
                                                             Log.InfoFormat("Processing Step: {0}", step.Name);

                                                             int startIndex = _repo.GetStartIndex(step.Name);
                                                             return current & step.Process(startIndex, _repo);
                                                         });
            return success;
        }
    }
}
