using NBatch.Core.Repositories;

namespace NBatch.Core;

/// <summary>
/// Extension methods for configuring Entity Framework Core-based job tracking on <see cref="JobBuilder"/>.
/// </summary>
public static class JobBuilderExtensions
{
    /// <summary>
    /// Enables SQL-backed job tracking for restart-from-failure support.
    /// Requires the <c>NBatch.EntityFrameworkCore</c> package.
    /// </summary>
    /// <param name="builder">The job builder.</param>
    /// <param name="connectionString">Database connection string.</param>
    /// <param name="provider">The database provider to use.</param>
    public static JobBuilder UseJobStore(this JobBuilder builder, string connectionString, DatabaseProvider provider = DatabaseProvider.SqlServer)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        builder.SetJobRepository(new EfJobRepository(builder.JobName, connectionString, provider));
        return builder;
    }
}
