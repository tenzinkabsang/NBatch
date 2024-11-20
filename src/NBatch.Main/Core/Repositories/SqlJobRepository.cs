using NBatch.Main.Common;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Dapper;
using Microsoft.Data.SqlClient;
using System.Threading.Tasks;

namespace NBatch.Main.Core.Repositories;

internal sealed class SqlJobRepository : IJobRepository
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

    public async Task CreateJobRecord(ICollection<string> stepNames)
    {
        await _db.ExecuteQueryAsync(async cmd =>
                         {
                             var currentTime = DateTime.UtcNow;
                             cmd.CommandText = "UPDATE BatchJob SET LastRun = @LastRun WHERE JobName = @JobName";
                             cmd.Parameters.AddWithValue("@JobName", _jobName);
                             cmd.Parameters.AddWithValue("@LastRun", currentTime);
                             int result = await cmd.ExecuteNonQueryAsync();
                             if (result != 0)
                                 return result;

                             cmd.CommandText = "INSERT INTO BatchJob (JobName, LastRun) VALUES (@JobName, @LastRun)";
                             await cmd.ExecuteNonQueryAsync();

                             return await CreateInitialStepRecordsAsync(stepNames, cmd);
                         });
    }

    private async Task<int> CreateInitialStepRecordsAsync(ICollection<string> stepNames, SqlCommand cmd)
    {
        cmd.Parameters.Clear();
        cmd.CommandText = """
            INSERT INTO BatchStep (StepName, JobName, StepIndex, NumberOfItemsProcessed)
            VALUES (@StepName, @JobName, @StepIndex, @NumberOfItemsProcessed)
            """;

        const int INITIAL_STATE = 0;
        cmd.Parameters.AddWithValue("@JobName", _jobName);
        cmd.Parameters.AddWithValue("@StepIndex", INITIAL_STATE);
        cmd.Parameters.AddWithValue("@NumberOfItemsProcessed", INITIAL_STATE);
        int rowsAffected = 0;
        foreach (var stepName in stepNames)
        {
            var sqlParam = new SqlParameter("StepName", SqlDbType.NVarChar) { Value = stepName };
            cmd.Parameters.Add(sqlParam);
            rowsAffected += await cmd.ExecuteNonQueryAsync();
            cmd.Parameters.Remove(sqlParam);
        }
        return rowsAffected;
    }
    
    public async Task<long> InsertStepAsync(string stepName, long stepIndex)
    {
        const string query = """
            INSERT INTO BatchStep (StepName, JobName, StepIndex, NumberOfItemsProcessed)
            VALUES (@StepName, @JobName, @StepIndex, 0); SELECT SCOPE_IDENTITY();"
            """;

        return await _db.ExecuteQueryAsync(async cmd =>
                                {
                                    cmd.CommandText = query;
                                    cmd.Parameters.AddWithValue("@StepName", stepName);
                                    cmd.Parameters.AddWithValue("@StepIndex", stepIndex);
                                    cmd.Parameters.AddWithValue("@JobName", _jobName);
                                    object id = await cmd.ExecuteScalarAsync();
                                    return long.Parse(id.ToString());
                                });
    }

    public async Task<long> UpdateStepAsync(long stepId, int numberOfItemsProcessed, bool error, bool skipped)
    {
        const string query = """
            Update BatchStep SET 
              NumberOfItemsProcessed = @NumberOfItemsProcessed, 
              Error = @Error, 
              Skipped = @Skipped 
            WHERE Id = @StepId"
            """;

        return await _db.ExecuteQueryAsync(async cmd =>
                                {
                                    cmd.CommandText = query;
                                    cmd.Parameters.AddWithValue("@NumberOfItemsProcessed", numberOfItemsProcessed);
                                    cmd.Parameters.AddWithValue("@Error", error);
                                    cmd.Parameters.AddWithValue("@Skipped", skipped);
                                    cmd.Parameters.AddWithValue("@StepId", stepId);
                                    return await cmd.ExecuteNonQueryAsync();
                                });
    }
    
    public async Task<StepContext> GetStartIndexAsync(string stepName)
    {
        const string query = """
            Select TOP 1 
              Id, 
              StepName, 
              JobName, 
              StepIndex, 
              NumberOfItemsProcessed 
            FROM BatchStep
            WHERE StepName = @StepName AND JobName = @JobName
            ORDER BY Id DESC
            """;


        return await _db.ExecuteQueryAsync(async (conn, trax) =>
        {
            var result = await conn.QueryAsync<StepContext>(query, new { StepName = stepName, JobName = _jobName }, trax);
            return result.FirstOrDefault();
        });
                    
    }

    public async Task SaveExceptionInfo(SkipContext skipContext, int exceptionCount)
    {
        const string insert = """
            INSERT INTO BatchStepException (StepIndex, StepName, JobName, ExceptionMsg, ExceptionDetails)
            VALUES (@StepIndex, @StepName, @JobName, @ExMsg, @ExDetails)
            """;

        await _db.ExecuteQueryAsync(async cmd =>
                         {
                             cmd.CommandText = insert;
                             cmd.Parameters.AddWithValue("@StepIndex", skipContext.StepIndex);
                             cmd.Parameters.AddWithValue("@StepName", skipContext.StepName);
                             cmd.Parameters.AddWithValue("@JobName", _jobName);
                             cmd.Parameters.AddWithValue("@ExMsg", skipContext.Exception.Message);
                             cmd.Parameters.AddWithValue("@ExDetails", skipContext.Exception.StackTrace);
                             return await cmd.ExecuteNonQueryAsync();
                         });
    }

    public async Task<int> GetExceptionCountAsync(SkipContext skipContext)
    {
        return await _db.ExecuteQueryAsync(async cmd =>
                                {
                                    cmd.CommandText = "SELECT COUNT(1) FROM BatchStepException WHERE StepName = @StepName AND JobName = @JobName";
                                    cmd.Parameters.AddWithValue("@StepName", skipContext.StepName);
                                    cmd.Parameters.AddWithValue("@JobName", _jobName);
                                    return (int)await cmd.ExecuteScalarAsync();
                                });
    }
}