using NBatch.Main.Common;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace NBatch.Main.Core.Repositories
{
    sealed class SqlJobRepository : IJobRepository
    {
        private readonly IDb _db;
        private readonly string _jobName;

        public SqlJobRepository(string jobName, string connectionStringName)
            : this(jobName, new Db(connectionStringName))
        {
        }

        internal SqlJobRepository(string jobName, IDb db)
        {
            _jobName = jobName;
            _db = db;
        }

        public void CreateJobRecord(ICollection<string> stepNames)
        {
            _db.ExecuteQuery(cmd =>
                             {
                                 var currentTime = DateTime.Now;
                                 cmd.CommandText = "Update BatchJob set LastRun = @LastRun where JobName = @JobName";
                                 cmd.Parameters.AddWithValue("@JobName", _jobName);
                                 cmd.Parameters.AddWithValue("@LastRun", currentTime);
                                 int result = cmd.ExecuteNonQuery();
                                 if (result != 0)
                                     return result;

                                 cmd.CommandText = "Insert into BatchJob (JobName, CreateDate, LastRun) values (@JobName, @CreateDate, @LastRun)";
                                 cmd.Parameters.AddWithValue("@CreateDate", currentTime);
                                 cmd.ExecuteNonQuery();

                                 return CreateInitialStepRecords(stepNames, cmd);
                             });
        }

        private int CreateInitialStepRecords(ICollection<string> stepNames, SqlCommand cmd)
        {
            cmd.Parameters.Clear();
            var currentTime = DateTime.Now;

            cmd.CommandText = "Insert into BatchStep (StepName, StepIndex, NumberOfItemsProcessed, JobName, LastRun) values " +
                              "(@StepName, @StepIndex, @NumberOfItemsProcessed, @JobName, @LastRun)";

            const int INITIAL_STATE = 0;
            cmd.Parameters.AddWithValue("@StepIndex", INITIAL_STATE);
            cmd.Parameters.AddWithValue("@NumberOfItemsProcessed", INITIAL_STATE);
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

        public void SaveStepContext(StepContext stepContext)
        {
            const string query =
                "Insert into BatchStep (StepName, StepIndex, NumberOfItemsProcessed, JobName, LastRun) values " +
                "(@StepName, @StepIndex, @NumberOfItemsProcessed, @JobName, @LastRun)";

            _db.ExecuteQuery(cmd =>
                             {
                                 cmd.CommandText = query;
                                 cmd.Parameters.AddWithValue("@StepName", stepContext.StepName);
                                 cmd.Parameters.AddWithValue("@StepIndex", stepContext.StepIndex);
                                 cmd.Parameters.AddWithValue("@NumberOfItemsProcessed",
                                     stepContext.NumberOfItemsProcessed);
                                 cmd.Parameters.AddWithValue("@JobName", _jobName);
                                 cmd.Parameters.AddWithValue("@LastRun", DateTime.Now);
                                 return cmd.ExecuteNonQuery();
                             });
        }

        public long GetStartIndex(string stepName)
        {
            return _db.ExecuteQuery(cmd =>
                                    {
                                        cmd.CommandText =
                                            "Select Max(StepIndex) from BatchStep where StepName = @StepName and JobName = @JobName";
                                        cmd.Parameters.AddWithValue("@StepName", stepName);
                                        cmd.Parameters.AddWithValue("@JobName", _jobName);
                                        return (long) cmd.ExecuteScalar();
                                    });
        }

        public void SaveExceptionInfo(SkipContext skipContext, int exceptionCount)
        {
            const string insert =
                "Insert into BatchStepException (StepName, LineNumber, ExceptionMsg, ExceptionDetails, JobName, CreateDate) " +
                "Values (@StepName, @LineNumber, @ExMsg, @ExDetails, @JobName, @CreateDate)";

            _db.ExecuteQuery(cmd =>
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
            return _db.ExecuteQuery(cmd =>
                                    {
                                        cmd.CommandText =
                                            "Select Count(*) from BatchStepException where StepName = @StepName and JobName = @JobName";
                                        cmd.Parameters.AddWithValue("@StepName", skipContext.StepName);
                                        cmd.Parameters.AddWithValue("@JobName", _jobName);
                                        return (int) cmd.ExecuteScalar();
                                    });
        }
    }
}