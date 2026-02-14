using Microsoft.EntityFrameworkCore;

namespace NBatch.ConsoleApp;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductLowercase> Destination => Set<ProductLowercase>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.ToTable("Product");
        });

        modelBuilder.Entity<ProductLowercase>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.ToTable("ProductLowercase");
        });
    }

    public static AppDbContext Create(string connectionString)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new AppDbContext(options);
    }
}
