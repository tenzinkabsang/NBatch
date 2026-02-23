using Microsoft.EntityFrameworkCore;

namespace NBatch.Tests.Integration.Fixtures;

/// <summary>
/// EF Core DbContext for integration test data.
/// Configurable for SQL Server or PostgreSQL via static factory methods.
/// </summary>
internal sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
{
    public DbSet<TestRecord> TestRecords => Set<TestRecord>();
    public DbSet<TestRecordEtl> TestRecordEtls => Set<TestRecordEtl>();

    internal static DbContextOptions<TestDbContext> ForSqlServer(string connectionString)
        => new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlServer(connectionString)
            .Options;

    internal static DbContextOptions<TestDbContext> ForPostgreSql(string connectionString)
        => new DbContextOptionsBuilder<TestDbContext>()
            .UseNpgsql(connectionString)
            .Options;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TestRecord>(entity =>
        {
            entity.ToTable("TestRecord");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.Code).HasMaxLength(50);
            entity.Property(e => e.Value).HasPrecision(18, 2);
            entity.Property(e => e.Category).HasMaxLength(50);
        });

        modelBuilder.Entity<TestRecordEtl>(entity =>
        {
            entity.ToTable("TestRecordEtl");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.Code).HasMaxLength(50);
            entity.Property(e => e.Value).HasPrecision(18, 2);
            entity.Property(e => e.Category).HasMaxLength(50);
        });
    }
}
