using Microsoft.EntityFrameworkCore;
using NBatch.Core.Interfaces;

namespace NBatch.Writers.DbWriter;

/// <summary>
/// Writes items to any EF Core <see cref="DbContext"/>.
/// Provider-agnostic — works with SQL Server, PostgreSQL, SQLite, etc.
/// </summary>
/// <typeparam name="TItem">The entity type to write. Must be registered in the <see cref="DbContext"/>.</typeparam>
/// <param name="dbContext">The EF Core <see cref="DbContext"/> to write to.</param>
public sealed class DbWriter<TItem>(DbContext dbContext) : IWriter<TItem>
    where TItem : class
{
    public async Task WriteAsync(IEnumerable<TItem> items, CancellationToken cancellationToken = default)
    {
        dbContext.Set<TItem>().AddRange(items);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
