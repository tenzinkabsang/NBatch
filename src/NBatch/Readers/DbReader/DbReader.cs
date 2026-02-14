using Microsoft.EntityFrameworkCore;
using NBatch.Core.Interfaces;

namespace NBatch.Readers.DbReader;

/// <summary>
/// Reads items from any EF Core <see cref="DbContext"/> in paginated chunks.
/// Provider-agnostic — works with SQL Server, PostgreSQL, SQLite, etc.
/// </summary>
/// <typeparam name="TItem">The entity type to read. Must be registered in the <see cref="DbContext"/>.</typeparam>
/// <param name="dbContext">The EF Core <see cref="DbContext"/> to query.</param>
/// <param name="queryBuilder">
/// A function that applies ordering (and optional filtering) to the queryable.
/// An ORDER BY clause is required for deterministic pagination.
/// </param>
public sealed class DbReader<TItem>(
    DbContext dbContext,
    Func<IQueryable<TItem>, IQueryable<TItem>> queryBuilder) : IReader<TItem>
    where TItem : class
{
    /// <inheritdoc />
    public async Task<IEnumerable<TItem>> ReadAsync(long startIndex, int chunkSize, CancellationToken cancellationToken = default)
    {
        return await queryBuilder(dbContext.Set<TItem>())
            .Skip((int)startIndex)
            .Take(chunkSize)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }
}
