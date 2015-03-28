using System;
using System.Data;
using System.Data.SqlClient;

namespace NBatch.Main.Common
{
    interface IDb
    {
        T ExecuteQuery<T>(Func<SqlCommand, T> operation);
        T ExecuteQuery<T>(Func<IDbConnection, IDbTransaction, T> operation);
    }
}