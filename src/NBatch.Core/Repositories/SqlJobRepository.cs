using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;

namespace NBatch.Core.Repositories
{
    sealed class SqlJobRepository : IJobRepository
    {
        private readonly string _jobName;
        private readonly string _connStr;

        public SqlJobRepository(string jobName, string conn)
        {
            _jobName = jobName;
            _connStr = ConfigurationManager.ConnectionStrings[conn].ConnectionString;
        }

        public void CreateJobRecord(ICollection<string> stepNames)
        {
            ExecuteQuery(cmd =>
                         {
                             var currentTime = DateTime.Now;
                             cmd.CommandText = "Update BatchJob set LastRun = @LastRun where JobName = @JobName";
                             cmd.Parameters.AddWithValue("@JobName", _jobName);
                             cmd.Parameters.AddWithValue("@LastRun", currentTime);
                             int result = cmd.ExecuteNonQuery();
                             if (result != 0)
                                 return result;

                             cmd.CommandText =
                                 "Insert into BatchJob (JobName, CreateDate, LastRun) values (@JobName, @CreateDate, @LastRun)";
                             cmd.Parameters.AddWithValue("@CreateDate", currentTime);
                             cmd.ExecuteNonQuery();

                             return CreateInitialStepRecords(stepNames, cmd);
                         });
        }

        private int CreateInitialStepRecords(ICollection<string> stepNames, SqlCommand cmd)
        {
            cmd.Parameters.Clear();
            var currentTime = DateTime.Now;

            cmd.CommandText = "Insert into BatchStep (StepName, StepIndex, JobName, LastRun) values " +
                                              "(@StepName, @StepIndex, @JobName, @LastRun)";
            cmd.Parameters.AddWithValue("@StepIndex", 0);
            cmd.Parameters.AddWithValue("@JobName", _jobName);
            cmd.Parameters.AddWithValue("@LastRun", currentTime);
            int rowsAffected = 0;
            foreach (var stepName in stepNames)
            {
                var sqlParam = new SqlParameter("StepName", SqlDbType.NVarChar) { Value = stepName };
                cmd.Parameters.Add(sqlParam);
                rowsAffected += cmd.ExecuteNonQuery();
                cmd.Parameters.Remove(sqlParam);
            }
            return rowsAffected;
        }

        public void SaveIndex(string stepName, long index)
        {
            ExecuteQuery(cmd =>
                         {
                             cmd.CommandText = "Insert into BatchStep (StepName, StepIndex, JobName, LastRun) values " +
                                               "(@StepName, @StepIndex, @JobName, @LastRun)";
                             cmd.Parameters.AddWithValue("@StepName", stepName);
                             cmd.Parameters.AddWithValue("@StepIndex", index);
                             cmd.Parameters.AddWithValue("@JobName", _jobName);
                             cmd.Parameters.AddWithValue("@LastRun", DateTime.Now);
                             return cmd.ExecuteNonQuery();
                         });
        }

        public long GetStartIndex(string stepName)
        {
            return ExecuteQuery(cmd =>
            {
                cmd.CommandText = "Select Max(StepIndex) from BatchStep where StepName = @StepName and JobName = @JobName";
                cmd.Parameters.AddWithValue("@StepName", stepName);
                cmd.Parameters.AddWithValue("@JobName", _jobName);
                return (long)cmd.ExecuteScalar();
            });
        }

        public void SaveExceptionInfo(SkipContext skipContext, int exceptionCount)
        {
            const string insert =
                "Insert into BatchStepException (StepName, LineNumber, ExceptionMsg, ExceptionDetails, JobName, CreateDate) " +
                "Values (@StepName, @LineNumber, @ExMsg, @ExDetails, @JobName, @CreateDate)";

            ExecuteQuery(cmd =>
            {
                cmd.CommandText = insert;
                cmd.Parameters.AddWithValue("@StepName", skipContext.StepName);
                cmd.Parameters.AddWithValue("@LineNumber", skipContext.LineNumber);
                cmd.Parameters.AddWithValue("@ExMsg", skipContext.Exception.Message);
                cmd.Parameters.AddWithValue("@ExDetails", skipContext.Exception.StackTrace);
                cmd.Parameters.AddWithValue("@JobName", _jobName);
                cmd.Parameters.AddWithValue("@CreateDate", DateTime.Now);
                return cmd.ExecuteNonQuery();
            });
        }

        public int GetExceptionCount(SkipContext skipContext)
        {
            return ExecuteQuery(cmd =>
                              {
                                  cmd.CommandText = "Select Count(*) from BatchStepException where StepName = @StepName and JobName = @JobName";
                                  cmd.Parameters.AddWithValue("@StepName", skipContext.StepName);
                                  cmd.Parameters.AddWithValue("@JobName", _jobName);
                                  return (int)cmd.ExecuteScalar();
                              });
        }




        private T ExecuteQuery<T>(Func<SqlCommand, T> operation)
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
    }
}