using NBatch.Core.Repositories;
using System.Collections.Generic;
using System.Linq;

namespace NBatch.Core
{
    public sealed class Job
    {
        private readonly IJobRepository _repo;
        private readonly IDictionary<string, IStep> _steps;

        public Job()
            : this(new JobRepository())
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

        //public Job AddStep(IStep newStep, IStep dependsOn)
        //{
        //    Ensure.UniqueStepName(_steps.Keys, newStep);

        //    // add dependency...??
        //    IStep dependedOn = _steps[dependsOn.Name];
        //    _steps.Remove(dependsOn.Name);



        //    return this;
        //}

        public bool Start()
        {
            // Aggregate: same as reduce(initialValue, (first, second) -> function)
            bool success = _steps.Values.Aggregate(true, (current, step) =>
                                                  {
                                                      int startIndex = _repo.GetStartIndex(step.Name);
                                                      return current & step.Process(startIndex, _repo);
                                                  });
            return success;
        }
    }
}
