using Dapper;
using Microsoft.Data.SqlClient;
using NBatch.Core.Interfaces;

namespace NBatch.Readers.SqlReader;

public sealed class MsSqlReader<TItem>(string connectionString, string sql) : IReader<TItem>
{
    private readonly string _sql = ValidateSql(sql);

    public async Task<IEnumerable<TItem>> ReadAsync(long startIndex, int chunkSize)
    {
        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        string sqlWithPagination = $"{_sql} OFFSET((@PageNumber-1) * @RowsPerPage) Rows FETCH NEXT @RowsPerPage ROWS ONLY";

        long pageNumber = (startIndex / chunkSize) + 1;
        return await connection.QueryAsync<TItem>(sqlWithPagination, new { PageNumber = pageNumber, RowsPerPage = chunkSize });
    }

    private static string ValidateSql(string sql)
    {
        ArgumentNullException.ThrowIfNull(sql);

        if (!sql.Contains("ORDER BY", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("SQL query must contain an ORDER BY clause for pagination.", nameof(sql));

        return sql;
    }
}
