namespace NBatch.Core;

/// <summary>
/// Configures batch jobs for dependency injection.
/// Passed to <see cref="Microsoft.Extensions.DependencyInjection.ServiceCollectionExtensions.AddNBatch"/>.
/// </summary>
public sealed class NBatchBuilder
{
    internal Dictionary<string, Func<IServiceProvider, Job>> Factories { get; } = [];
    internal List<JobRegistration> Registrations { get; } = [];

    /// <summary>Registers a named job.</summary>
    /// <param name="jobName">A unique name that identifies this job.</param>
    /// <param name="configure">A delegate that configures the job via <see cref="JobBuilder"/>.</param>
    /// <returns>A <see cref="JobRegistration"/> that can optionally configure a schedule.</returns>
    public JobRegistration AddJob(string jobName, Action<JobBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(jobName);
        ArgumentNullException.ThrowIfNull(configure);

        Factories[jobName] = _ =>
        {
            var builder = new JobBuilder(jobName);
            configure(builder);
            return builder.Build();
        };

        var registration = new JobRegistration(jobName);
        Registrations.Add(registration);
        return registration;
    }

    /// <summary>
    /// Registers a named job with access to the service provider for resolving dependencies.
    /// </summary>
    /// <param name="jobName">A unique name that identifies this job.</param>
    /// <param name="configure">A delegate that receives <see cref="IServiceProvider"/> and <see cref="JobBuilder"/>.</param>
    /// <returns>A <see cref="JobRegistration"/> that can optionally configure a schedule.</returns>
    public JobRegistration AddJob(string jobName, Action<IServiceProvider, JobBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(jobName);
        ArgumentNullException.ThrowIfNull(configure);

        Factories[jobName] = sp =>
        {
            var builder = new JobBuilder(jobName);
            configure(sp, builder);
            return builder.Build();
        };

        var registration = new JobRegistration(jobName);
        Registrations.Add(registration);
        return registration;
    }
}
