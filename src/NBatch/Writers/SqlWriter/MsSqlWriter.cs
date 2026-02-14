using Dapper;
using Microsoft.Data.SqlClient;
using NBatch.Core.Interfaces;

namespace NBatch.Writers.SqlWriter;

public sealed class MsSqlWriter<TItem>(string connectionString, string sql) : IWriter<TItem>
{
    public async Task WriteAsync(IEnumerable<TItem> items, CancellationToken cancellationToken = default)
    {
        var itemList = items.ToList();

        if (itemList.Count == 0)
            return;

        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var command = new CommandDefinition(sql, itemList, transaction, cancellationToken: cancellationToken);
        int result = await connection.ExecuteAsync(command);

        if (result != itemList.Count)
            throw new InvalidOperationException($"Expected to write {itemList.Count} items but only wrote {result}.");

        await transaction.CommitAsync(cancellationToken);
    }
}
