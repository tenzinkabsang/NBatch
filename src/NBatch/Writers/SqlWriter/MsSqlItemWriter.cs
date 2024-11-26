using Dapper;
using Microsoft.Data.SqlClient;
using NBatch.Core.Interfaces;

namespace NBatch.Writers.SqlWriter;

internal sealed class MsSqlItemWriter<TItem>(string connectionString, string query) : IWriter<TItem>
{
    public async Task<bool> WriteAsync(IEnumerable<TItem> items)
    {
        if (!items.Any())
            return false;

        using var connection = new SqlConnection(connectionString);
        using var tranx = await connection.BeginTransactionAsync();

        int result = await connection.ExecuteAsync(query, items.ToArray(), tranx);
        return result == items.Count();
    }
}
