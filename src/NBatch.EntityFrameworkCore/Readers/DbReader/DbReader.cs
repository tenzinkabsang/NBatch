using System.Linq.Expressions;
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
/// re-reads performed when a chunk fails — rely on stable row positions. The
/// first read throws <see cref="InvalidOperationException"/> if no ordering was applied.
/// </param>
public sealed class DbReader<TItem>(
    DbContext dbContext,
    Func<IQueryable<TItem>, IQueryable<TItem>> queryBuilder) : IReader<TItem>
    where TItem : class
{
    private bool _orderingVerified;

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The built query has no OrderBy clause.</exception>
    public async Task<IEnumerable<TItem>> ReadAsync(long startIndex, int chunkSize, CancellationToken cancellationToken = default)
    {
        if (startIndex > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(startIndex), startIndex,
                "DbReader supports start indexes up to int.MaxValue because EF Core's Skip() takes an int.");

        var query = queryBuilder(dbContext.Set<TItem>());

        if (!_orderingVerified)
        {
            if (!HasOrdering(query.Expression))
                throw new InvalidOperationException(
                    $"DbReader<{typeof(TItem).Name}> requires the query to apply a deterministic ordering " +
                    "(OrderBy/OrderByDescending): Skip/Take pagination and restart-from-failure rely on stable " +
                    "row positions, and an unordered SQL query may return rows in any order. " +
                    "Apply an ordering in the queryBuilder, e.g. q => q.OrderBy(x => x.Id).");
            _orderingVerified = true;
        }

        return await query
            .Skip((int)startIndex)
            .Take(chunkSize)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    private static bool HasOrdering(Expression expression)
    {
        var detector = new OrderingDetector();
        detector.Visit(expression);
        return detector.Found;
    }

    private sealed class OrderingDetector : ExpressionVisitor
    {
        public bool Found { get; private set; }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (node.Method.DeclaringType == typeof(Queryable) &&
                node.Method.Name is nameof(Queryable.OrderBy) or nameof(Queryable.OrderByDescending)
                                 or nameof(Queryable.ThenBy) or nameof(Queryable.ThenByDescending)
                                 or nameof(Queryable.Order) or nameof(Queryable.OrderDescending))
            {
                Found = true;
                return node;
            }

            return base.VisitMethodCall(node);
        }
    }
}
