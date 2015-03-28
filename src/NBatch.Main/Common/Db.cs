using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;

namespace NBatch.Main.Common
{
    class Db : IDb
    {
        private readonly string _connStr;

        public Db(string conn)
        {
            _connStr = ConfigurationManager.ConnectionStrings[conn].ConnectionString;
        }

        public T ExecuteQuery<T>(Func<SqlCommand, T> operation)
        {
            using (var conn = new SqlConnection(_connStr))
            {
                conn.Open();
                using (var tranx = conn.BeginTransaction())
                {
                    try
                    {
                        SqlCommand command = conn.CreateCommand();
                        command.Transaction = tranx;

                        T result = operation(command);
                        tranx.Commit();
                        return result;
                    }
                    catch (Exception)
                    {
                        tranx.Rollback();
                        throw;
                    }
                }
            }
        }

        public T ExecuteQuery<T>(Func<IDbConnection, IDbTransaction, T> operation)
        {
            using (var conn = new SqlConnection(_connStr))
            {
                conn.Open();
                using (var tranx = conn.BeginTransaction())
                {
                    try
                    {
                        T result = operation(conn, tranx);
                        tranx.Commit();
                        return result;
                    }
                    catch (Exception)
                    {
                        tranx.Rollback();
                        throw;
                    }
                }
            }
        }
    }
}
