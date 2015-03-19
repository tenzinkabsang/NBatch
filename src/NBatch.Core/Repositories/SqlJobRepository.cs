using System;
using System.Data.SqlClient;

namespace NBatch.Core.Repositories
{
    sealed class SqlJobRepository : IJobRepository
    {
        private readonly string _jobName;
        private readonly string _conn;

        public SqlJobRepository(string jobName, string conn)
        {
            _jobName = jobName;
            _conn = conn;
        }

        public void SaveIndex(string stepName, int index)
        {
            ExecuteQuery(db =>
                         {
                             var cmd = db.CreateCommand();
                             cmd.CommandText = "Update BatchStep set Index = @Index where StepName = @StepName";
                             cmd.Parameters.AddWithValue("Index", index);
                             cmd.Parameters.AddWithValue("@StepName", stepName);
                             int result = cmd.ExecuteNonQuery();
                             if (result != 0)
                                 return result;

                             cmd.CommandText = "Insert into BatchStep (StepName, Index) values (@StepName, Index)";
                             return cmd.ExecuteNonQuery();
                         });
        }

        public int GetExceptionCount(SkipContext skipContext)
        {
            return ExecuteQuery(db =>
                              {
                                  var cmd = db.CreateCommand();
                                  cmd.CommandText = "Select ExceptionCount from BatchStepException where StepName = @StepName";
                                  cmd.Parameters.AddWithValue("@StepName", skipContext.StepName);
                                  return (int)cmd.ExecuteScalar();
                              });
        }

        public void IncrementExceptionCount(SkipContext skipContext, int exceptionCount)
        {
            const string insert =
                "Insert into BatchStepException (LineNumber, ExceptionCount, ExceptionMsg, ExceptionDetails) " +
                "Values (@LineNumber, @ExCount, @ExMsg, @ExDetails)";

            const string update =
                "Update BatchStepException Set LineNumber = @LineNumber, ExceptionCount = @ExCount, ExceptionMsg = @ExMsg, ExceptionDetails = @ExDetails";
            ExecuteQuery(db =>
                         {
                             var cmd = db.CreateCommand();
                             cmd.CommandText = update;
                             cmd.Parameters.AddWithValue("@LineNumber", skipContext.LineNumber);
                             cmd.Parameters.AddWithValue("@ExCount", exceptionCount);
                             cmd.Parameters.AddWithValue("@ExMsg", skipContext.Exception.Message);
                             cmd.Parameters.AddWithValue("@ExDetails", skipContext.Exception.StackTrace);
                             int result = cmd.ExecuteNonQuery();
                             if (result != 0)
                                 return result;

                             cmd.CommandText = insert;
                             return cmd.ExecuteNonQuery();
                         });
        }

        public int GetStartIndex(string stepName)
        {
            return ExecuteQuery(db =>
                                {
                                    var cmd = db.CreateCommand();
                                    cmd.CommandText = "Select Max(Index) from BatchStep where StepName = @StepName";
                                    cmd.Parameters.AddWithValue("@StepName", stepName);
                                    return (int) cmd.ExecuteScalar();
                                });
        }

        private T ExecuteQuery<T>(Func<SqlConnection, T> operation)
        {
            using (var conn = new SqlConnection(_conn))
            {
                conn.Open();
                using (var tranx = conn.BeginTransaction())
                {
                    try
                    {
                        T result = operation(conn);
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