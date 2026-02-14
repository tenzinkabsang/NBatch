using Microsoft.EntityFrameworkCore;
using NBatch.Core.Repositories.Entities;

namespace NBatch.Core.Repositories;

internal sealed class NBatchDbContext(DbContextOptions<NBatchDbContext> options) : DbContext(options)
{
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
            .HasMaxLength(100);
            
            entity.Property(e => e.CreateDate)
            .HasColumnName("create_date");
            
            entity.Property(e => e.LastRun)
            .HasColumnName("last_run");
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
            .HasMaxLength(100);
            
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
            entity.HasIndex(e => e.StepName);
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
            .HasMaxLength(100);
            
            entity.Property(e => e.JobName)
            .HasColumnName("job_name")
            .HasMaxLength(100);
            
            entity.Property(e => e.ExceptionMsg)
            .HasColumnName("exception_msg")
            .HasMaxLength(500);
            
            entity.Property(e => e.ExceptionDetails)
            .HasColumnName("exception_details")
            .HasMaxLength(1500);
            
            entity.Property(e => e.CreateDate)
            .HasColumnName("create_date");
            
            entity.HasOne<JobEntity>()
                .WithMany()
                .HasForeignKey(e => e.JobName);
            
            entity.HasIndex(e => e.StepName);
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
            _ => throw new ArgumentOutOfRangeException(nameof(provider))
        };

        return builder.Options;
    }
}
