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
/// A deterministic ORDER BY clause is required: pagination — and the item-level
/// re-reads performed when a chunk fails — rely on stable row positions.
/// </param>
public sealed class DbReader<TItem>(
    DbContext dbContext,
    Func<IQueryable<TItem>, IQueryable<TItem>> queryBuilder) : IReader<TItem>
    where TItem : class
{
    /// <inheritdoc />
    public async Task<IEnumerable<TItem>> ReadAsync(long startIndex, int chunkSize, CancellationToken cancellationToken = default)
    {
        if (startIndex > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(startIndex), startIndex,
                "DbReader supports start indexes up to int.MaxValue because EF Core's Skip() takes an int.");

        return await queryBuilder(dbContext.Set<TItem>())
            .Skip((int)startIndex)
            .Take(chunkSize)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }
}
