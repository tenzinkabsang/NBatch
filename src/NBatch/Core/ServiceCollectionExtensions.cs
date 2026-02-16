using NBatch.Core;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering NBatch jobs with the dependency injection container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers NBatch services and configures batch jobs.
    /// <para>
    /// Jobs are built lazily when <see cref="IJobRunner.RunAsync"/> is called.
    /// </para>
    /// <example>
    /// <code>
    /// services.AddNBatch(nbatch =>
    /// {
    ///     nbatch.AddJob("csv-import", job => job
    ///         .AddStep("import", step => step
    ///             .ReadFrom(new CsvReader&lt;Product&gt;("data.csv", mapFn))
    ///             .WriteTo(new DbWriter&lt;Product&gt;(dbContext))
    ///             .WithChunkSize(100)));
    /// });
    /// </code>
    /// </example>
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">A delegate that registers jobs via <see cref="NBatchBuilder"/>.</param>
    public static IServiceCollection AddNBatch(
        this IServiceCollection services,
        Action<NBatchBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        var builder = new NBatchBuilder();
        configure(builder);

        services.AddSingleton<IJobRunner>(sp => new JobRunner(builder.Factories, sp));

        return services;
    }
}
