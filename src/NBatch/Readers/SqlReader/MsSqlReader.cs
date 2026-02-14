using Dapper;
using Microsoft.Data.SqlClient;
using NBatch.Core.Interfaces;

namespace NBatch.Readers.SqlReader;

public sealed class MsSqlReader<TItem>(string connectionString, string sql) : IReader<TItem>
{
    private readonly string _sql = ValidateSql(sql);

    public async Task<IEnumerable<TItem>> ReadAsync(long startIndex, int chunkSize, CancellationToken cancellationToken = default)
    {
        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        string sqlWithPagination = $"{_sql} OFFSET((@PageNumber-1) * @RowsPerPage) Rows FETCH NEXT @RowsPerPage ROWS ONLY";

        long pageNumber = (startIndex / chunkSize) + 1;
        var command = new CommandDefinition(sqlWithPagination, new { PageNumber = pageNumber, RowsPerPage = chunkSize }, cancellationToken: cancellationToken);
        return await connection.QueryAsync<TItem>(command);
    }

    private static string ValidateSql(string sql)
    {
        ArgumentNullException.ThrowIfNull(sql);

        if (!sql.Contains("ORDER BY", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("SQL query must contain an ORDER BY clause for pagination.", nameof(sql));

        return sql;
    }
}
