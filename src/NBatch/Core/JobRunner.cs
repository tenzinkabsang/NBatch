using Microsoft.Extensions.DependencyInjection;

namespace NBatch.Core;

/// <summary>
/// Resolves registered job factories and executes them.
/// Each <see cref="RunAsync"/> call creates a new <see cref="IServiceScope"/>,
/// ensuring scoped services (such as <c>DbContext</c>) receive a fresh instance
/// per job run and are properly disposed when the run completes.
/// </summary>
internal sealed class JobRunner(
    IReadOnlyDictionary<string, Func<IServiceProvider, Job>> factories,
    IServiceProvider serviceProvider) : IJobRunner
{
    public async Task<JobResult> RunAsync(string jobName, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(jobName);

        if (!factories.TryGetValue(jobName, out var factory))
            throw new ArgumentException($"No job registered with the name '{jobName}'.", nameof(jobName));

        await using var scope = serviceProvider.CreateAsyncScope();
        var job = factory(scope.ServiceProvider);
        return await job.RunAsync(cancellationToken);
    }
}
