using Microsoft.EntityFrameworkCore;
using NBatch.Core.Interfaces;

namespace NBatch.Writers.DbWriter;

/// <summary>
/// Writes items to any EF Core <see cref="DbContext"/>.
/// Provider-agnostic — works with SQL Server, PostgreSQL, SQLite, etc.
/// After each save the written entities are detached, so the change tracker does
/// not grow unboundedly over a long-running job. Entities the caller tracked
/// separately are left untouched.
/// </summary>
/// <typeparam name="TItem">The entity type to write. Must be registered in the <see cref="DbContext"/>.</typeparam>
/// <param name="dbContext">The EF Core <see cref="DbContext"/> to write to.</param>
public sealed class DbWriter<TItem>(DbContext dbContext) : IWriter<TItem>
    where TItem : class
{
    /// <inheritdoc />
    public async Task WriteAsync(IEnumerable<TItem> items, CancellationToken cancellationToken = default)
    {
        var list = items as IReadOnlyCollection<TItem> ?? items.ToList();

        dbContext.Set<TItem>().AddRange(list);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Detach only the entities written here — never ChangeTracker.Clear(),
        // which would also evict entities the caller is tracking on a shared context.
        foreach (var item in list)
            dbContext.Entry(item).State = EntityState.Detached;
    }
}
