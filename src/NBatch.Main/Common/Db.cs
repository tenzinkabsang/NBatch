using Microsoft.Data.SqlClient;
using System;
using System.Configuration;
using System.Data;
using System.Threading.Tasks;

namespace NBatch.Main.Common;

internal sealed class Db(string conn) : IDb
{
    private readonly string _connStr = ConfigurationManager.ConnectionStrings[conn].ConnectionString;

    public async Task<T> ExecuteQueryAsync<T>(Func<SqlCommand, Task<T>> operation)
    {
        using var conn = new SqlConnection(_connStr);
        conn.Open();
        using var tranx = conn.BeginTransaction();
        try
        {
            SqlCommand command = conn.CreateCommand();
            command.Transaction = tranx;

            T result = await operation(command);
            await tranx.CommitAsync();
            return result;
        }
        catch (Exception)
        {
            await tranx.RollbackAsync();
            throw;
        }
    }

    public async Task<T> ExecuteQueryAsync<T>(Func<IDbConnection, IDbTransaction, Task<T>> operation)
    {
        using var conn = new SqlConnection(_connStr);
        conn.Open();
        using var tranx = conn.BeginTransaction();
        try
        {
            T result = await operation(conn, tranx);
            await tranx.CommitAsync();
            return result;
        }
        catch (Exception)
        {
            await tranx.RollbackAsync();
            throw;
        }
    }
}
