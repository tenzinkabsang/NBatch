using Dapper;
using Microsoft.Data.SqlClient;
using NBatch.Core.Interfaces;

namespace NBatch.Writers.SqlWriter;

public sealed class MsSqlWriter<TItem>(string connectionString, string sql) : IWriter<TItem>
{
    public async Task<bool> WriteAsync(IEnumerable<TItem> items)
    {
        if (!items.Any())
            return false;

        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        using var tranx = await connection.BeginTransactionAsync();

        int result = await connection.ExecuteAsync(sql, items.ToArray(), tranx);
        await tranx.CommitAsync();
        return result == items.Count();
    }
}
