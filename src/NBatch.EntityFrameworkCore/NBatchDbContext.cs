using Microsoft.EntityFrameworkCore;
using NBatch.Core.Repositories.Entities;

namespace NBatch.Core.Repositories;

internal sealed class NBatchDbContext(DbContextOptions<NBatchDbContext> options) : DbContext(options)
{
    /// <summary>Column size for <c>exception_msg</c>; longer values are truncated before insert.</summary>
    internal const int MaxExceptionMsgLength = 500;

    /// <summary>Column size for <c>exception_details</c>; longer values are truncated before insert.</summary>
    internal const int MaxExceptionDetailLength = 5000;

    public DbSet<JobEntity> BatchJobs => Set<JobEntity>();
    public DbSet<StepEntity> BatchSteps => Set<StepEntity>();
    public DbSet<StepExceptionEntity> BatchStepExceptions => Set<StepExceptionEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("nbatch");

        modelBuilder.Entity<JobEntity>(entity =>
        {
            entity.ToTable("jobs");
            entity.HasKey(e => e.JobName);

            entity.Property(e => e.JobName)
            .HasColumnName("job_name")
            .HasMaxLength(500);
            
            entity.Property(e => e.CreateDate)
            .HasColumnName("create_date");
            
            entity.Property(e => e.LastRun)
            .HasColumnName("last_run");

            entity.Property(e => e.LastRunSuccess)
            .HasColumnName("last_run_success");
        });

        modelBuilder.Entity<StepEntity>(entity =>
        {
            entity.ToTable("steps");
            entity.HasKey(e => e.Id);
            
            entity.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();
            
            entity.Property(e => e.StepName)
            .HasColumnName("step_name")
            .HasMaxLength(100);
            
            entity.Property(e => e.JobName)
            .HasColumnName("job_name")
            .HasMaxLength(500);
            
            entity.Property(e => e.Error)
            .HasColumnName("error")
            .HasDefaultValue(false);
            
            entity.Property(e => e.Skipped)
            .HasColumnName("skipped")
            .HasDefaultValue(false);
            
            entity.Property(e => e.StepIndex)
            .HasColumnName("step_index");
            
            entity.Property(e => e.NumberOfItemsProcessed)
            .HasColumnName("number_of_items_processed");
            
            entity.Property(e => e.RunDate)
            .HasColumnName("run_date");
            
            entity.HasOne<JobEntity>()
                .WithMany()
                .HasForeignKey(e => e.JobName);
            // Serves GetStartIndexAsync: filter by (job, step), newest row first.
            entity.HasIndex(e => new { e.JobName, e.StepName, e.Id });
        });

        modelBuilder.Entity<StepExceptionEntity>(entity =>
        {
            entity.ToTable("step_exceptions");
            entity.HasKey(e => e.Id);
            
            entity.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();
            
            entity.Property(e => e.StepIndex)
            .HasColumnName("step_index");
            
            entity.Property(e => e.StepName)
            .HasColumnName("step_name")
            .HasMaxLength(500);
            
            entity.Property(e => e.JobName)
            .HasColumnName("job_name")
            .HasMaxLength(500);

            entity.Property(e => e.ExecutionId)
            .HasColumnName("execution_id");
            
            entity.Property(e => e.ExceptionMsg)
            .HasColumnName("exception_msg")
            .HasMaxLength(MaxExceptionMsgLength);

            entity.Property(e => e.ExceptionDetails)
            .HasColumnName("exception_details")
            .HasMaxLength(MaxExceptionDetailLength);
            
            entity.Property(e => e.CreateDate)
            .HasColumnName("create_date");
            
            entity.HasOne<JobEntity>()
                .WithMany()
                .HasForeignKey(e => e.JobName);

            // Serves GetExceptionCountAsync: filter by (job, step, execution).
            entity.HasIndex(e => new { e.JobName, e.StepName, e.ExecutionId });
        });
    }

    internal static DbContextOptions<NBatchDbContext> CreateOptions(string connectionString, DatabaseProvider provider)
    {
        var builder = new DbContextOptionsBuilder<NBatchDbContext>();

        _ = provider switch
        {
            DatabaseProvider.SqlServer => builder.UseSqlServer(connectionString),
            DatabaseProvider.PostgreSql => builder.UseNpgsql(connectionString),
            DatabaseProvider.Sqlite => builder.UseSqlite(connectionString),
#if NET8_0 || NET9_0
            DatabaseProvider.MySql => builder.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)),
#else
            DatabaseProvider.MySql => throw new PlatformNotSupportedException(
                "MySQL support requires Pomelo.EntityFrameworkCore.MySql, which does not yet support this .NET version. " +
                "Use .NET 8 or .NET 9, or check for an updated Pomelo release."),
#endif
            _ => throw new ArgumentOutOfRangeException(nameof(provider))
        };

        return builder.Options;
    }
}
