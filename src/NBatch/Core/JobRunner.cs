namespace NBatch.Core;

/// <summary>
/// Resolves registered job factories and executes them.
/// </summary>
internal sealed class JobRunner(
    IReadOnlyDictionary<string, Func<IServiceProvider, Job>> factories,
    IServiceProvider serviceProvider) : IJobRunner
{
    public Task<JobResult> RunAsync(string jobName, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(jobName);

        if (!factories.TryGetValue(jobName, out var factory))
            throw new ArgumentException($"No job registered with the name '{jobName}'.", nameof(jobName));

        var job = factory(serviceProvider);
        return job.RunAsync(cancellationToken);
    }
}
