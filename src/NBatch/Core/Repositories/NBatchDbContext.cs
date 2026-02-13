using Microsoft.EntityFrameworkCore;
using NBatch.Core.Repositories.Entities;

namespace NBatch.Core.Repositories;

internal sealed class NBatchDbContext(DbContextOptions<NBatchDbContext> options) : DbContext(options)
{
    public DbSet<BatchJobEntity> BatchJobs => Set<BatchJobEntity>();
    public DbSet<BatchStepEntity> BatchSteps => Set<BatchStepEntity>();
    public DbSet<BatchStepExceptionEntity> BatchStepExceptions => Set<BatchStepExceptionEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BatchJobEntity>(entity =>
        {
            entity.ToTable("BatchJob");
            entity.HasKey(e => e.JobName);
            entity.Property(e => e.JobName).HasMaxLength(100);
            entity.Property(e => e.CreateDate);
        });

        modelBuilder.Entity<BatchStepEntity>(entity =>
        {
            entity.ToTable("BatchStep");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.StepName).HasMaxLength(100);
            entity.Property(e => e.JobName).HasMaxLength(100);
            entity.Property(e => e.Error).HasDefaultValue(false);
            entity.Property(e => e.Skipped).HasDefaultValue(false);
            entity.HasOne<BatchJobEntity>()
                .WithMany()
                .HasForeignKey(e => e.JobName);
            entity.HasIndex(e => e.StepName);
        });

        modelBuilder.Entity<BatchStepExceptionEntity>(entity =>
        {
            entity.ToTable("BatchStepException");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.StepName).HasMaxLength(100);
            entity.Property(e => e.JobName).HasMaxLength(100);
            entity.Property(e => e.ExceptionMsg).HasMaxLength(500);
            entity.Property(e => e.ExceptionDetails).HasMaxLength(1500);
            entity.Property(e => e.CreateDate);
            entity.HasOne<BatchJobEntity>()
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
            DatabaseProvider.PostgreSql => builder.UseNpgsql(connectionString).UseSnakeCaseNamingConvention(),
            _ => throw new ArgumentOutOfRangeException(nameof(provider))
        };

        return builder.Options;
    }
}
