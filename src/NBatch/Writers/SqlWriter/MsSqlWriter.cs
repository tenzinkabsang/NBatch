using Dapper;
using Microsoft.Data.SqlClient;
using NBatch.Core.Interfaces;

namespace NBatch.Writers.SqlWriter;

public sealed class MsSqlWriter<TItem>(string connectionString, string sql) : IWriter<TItem>
{
    public async Task<bool> WriteAsync(IEnumerable<TItem> items)
    {
        var itemList = items.ToList();

        if (itemList.Count == 0)
            return false;

        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        using var transaction = await connection.BeginTransactionAsync();

        int result = await connection.ExecuteAsync(sql, itemList, transaction);
        await transaction.CommitAsync();
        return result == itemList.Count;
    }
}
