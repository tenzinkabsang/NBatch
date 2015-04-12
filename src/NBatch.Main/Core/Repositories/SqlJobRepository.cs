using NBatch.Main.Common;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using Dapper;

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
                                 var currentTime = DateTime.UtcNow;
                                 cmd.CommandText = "Update BatchJob set LastRun = @LastRun where JobName = @JobName";
                                 cmd.Parameters.AddWithValue("@JobName", _jobName);
                                 cmd.Parameters.AddWithValue("@LastRun", currentTime);
                                 int result = cmd.ExecuteNonQuery();
                                 if (result != 0)
                                     return result;

                                 cmd.CommandText = "Insert into BatchJob (JobName, LastRun) values (@JobName, @LastRun)";
                                 cmd.ExecuteNonQuery();

                                 return CreateInitialStepRecords(stepNames, cmd);
                             });
        }

        private int CreateInitialStepRecords(ICollection<string> stepNames, SqlCommand cmd)
        {
            cmd.Parameters.Clear();
            cmd.CommandText = "Insert into BatchStep (StepName, JobName, StepIndex, NumberOfItemsProcessed) values " +
                                                    "(@StepName, @JobName, @StepIndex, @NumberOfItemsProcessed)";

            const int INITIAL_STATE = 0;
            cmd.Parameters.AddWithValue("@JobName", _jobName);
            cmd.Parameters.AddWithValue("@StepIndex", INITIAL_STATE);
            cmd.Parameters.AddWithValue("@NumberOfItemsProcessed", INITIAL_STATE);
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
        
        public long InsertStep(string stepName, long stepIndex)
        {
            const string query =
                "Insert into BatchStep (StepName, JobName, StepIndex, NumberOfItemsProcessed) values " +
                                     "(@StepName, @JobName, @StepIndex, 0); SELECT SCOPE_IDENTITY();";

            return _db.ExecuteQuery(cmd =>
                                    {
                                        cmd.CommandText = query;
                                        cmd.Parameters.AddWithValue("@StepName", stepName);
                                        cmd.Parameters.AddWithValue("@StepIndex", stepIndex);
                                        cmd.Parameters.AddWithValue("@JobName", _jobName);
                                        object id = cmd.ExecuteScalar();
                                        return long.Parse(id.ToString());
                                    });
        }

        public long UpdateStep(long stepId, int numberOfItemsProcessed, bool error, bool skipped)
        {
            const string query =
                "Update BatchStep set NumberOfItemsProcessed = @NumberOfItemsProcessed, " +
                "Error = @Error, Skipped = @Skipped Where Id = @StepId";

            return _db.ExecuteQuery(cmd =>
                                    {
                                        cmd.CommandText = query;
                                        cmd.Parameters.AddWithValue("@NumberOfItemsProcessed", numberOfItemsProcessed);
                                        cmd.Parameters.AddWithValue("@Error", error);
                                        cmd.Parameters.AddWithValue("@Skipped", skipped);
                                        cmd.Parameters.AddWithValue("@StepId", stepId);
                                        return cmd.ExecuteNonQuery();
                                    });
        }
        
        public StepContext GetStartIndex(string stepName)
        {
            const string query =
                "Select top 1 Id, StepName, JobName, StepIndex, NumberOfItemsProcessed from BatchStep " +
                "Where StepName = @StepName and JobName = @JobName " +
                "Order By Id DESC";
            return _db.ExecuteQuery((conn, trax) =>
                        conn.Query<StepContext>(query, new {StepName = stepName, JobName = _jobName}, trax).FirstOrDefault());
        }

        public void SaveExceptionInfo(SkipContext skipContext, int exceptionCount)
        {
            const string insert =
                "Insert into BatchStepException (StepIndex, StepName, JobName, ExceptionMsg, ExceptionDetails) " +
                                        "Values (@StepIndex, @StepName, @JobName, @ExMsg, @ExDetails)";

            _db.ExecuteQuery(cmd =>
                             {
                                 cmd.CommandText = insert;
                                 cmd.Parameters.AddWithValue("@StepIndex", skipContext.StepIndex);
                                 cmd.Parameters.AddWithValue("@StepName", skipContext.StepName);
                                 cmd.Parameters.AddWithValue("@JobName", _jobName);
                                 cmd.Parameters.AddWithValue("@ExMsg", skipContext.Exception.Message);
                                 cmd.Parameters.AddWithValue("@ExDetails", skipContext.Exception.StackTrace);
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