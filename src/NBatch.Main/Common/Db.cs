using System;
using System.Data.SqlClient;

namespace NBatch.Main.Common
{
    public static class Db
    {
        public static T ExecuteQuery<T>(string connStr, Func<SqlCommand, T> operation)
        {
            using (var conn = new SqlConnection(connStr))
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
    }
}
