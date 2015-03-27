using System.Collections.Generic;
using System.Linq;
using NBatch.Main.Core.Repositories;

namespace NBatch.Main.Core
{
    public sealed class Job
    {
        private readonly IJobRepository _repo;
        private readonly IDictionary<string, IStep> _steps;

        public Job()
            : this(new InMemoryJobRepository())
        {
        }

        public Job(string jobName, string connectionStringName)
            :this(new SqlJobRepository(jobName, connectionStringName))
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
            _repo.CreateJobRecord(_steps.Keys);
            bool success = _steps.Values.Aggregate(true, (current, step) =>
                                                         {
                                                             long startIndex = _repo.GetStartIndex(step.Name);
                                                             return current & step.Process(startIndex, _repo);
                                                         });
            return success;
        }
    }


}
