using Microsoft.Data.SqlClient;
using System;
using System.Data;
using System.Threading.Tasks;

namespace NBatch.Main.Common;

internal interface IDb
{
    Task<T> ExecuteQueryAsync<T>(Func<SqlCommand, Task<T>> operation);
    Task<T> ExecuteQueryAsync<T>(Func<IDbConnection, IDbTransaction, Task<T>> operation);
}