using NBatch.Core.Interfaces;
using NBatch.Core.Repositories;

namespace NBatch.Core;

public sealed class Job
{
    private readonly string _jobName;
    private readonly IJobRepository _jobRepository;
    private readonly Dictionary<string, IStep> _steps = [];
    
    public Job(string jobName, IList<IStep> steps, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(jobName);
        ArgumentNullException.ThrowIfNull(steps);
        ArgumentNullException.ThrowIfNull(connectionString);
        Ensure.UniqueStepNames(steps);

        _jobName = jobName;
        _steps = steps.ToDictionary(s => s.Name);
        
    }

}
