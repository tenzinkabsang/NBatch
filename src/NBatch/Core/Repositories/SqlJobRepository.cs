using Dapper;
using Microsoft.Data.SqlClient;

namespace NBatch.Core.Repositories;

internal sealed class SqlJobRepository(string jobName, string connectionString) : IJobRepository
{
    public async Task CreateJobRecordAsync(ICollection<string> stepNames)
    {
        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        using var transaction = await connection.BeginTransactionAsync();
        
        var job = await connection.QuerySingleOrDefaultAsync<string>(@"SELECT JobName FROM BatchJob WHERE JobName = @JobName", new { JobName = jobName }, transaction);

        if (!string.IsNullOrEmpty(job))
            await connection.ExecuteAsync(@"UPDATE BatchJob SET LastRun = @LastRun WHERE JobName = @JobName", new { LastRun = DateTime.UtcNow, JobName = jobName }, transaction);
        else
        {
            await connection.ExecuteAsync(@"INSERT INTO BatchJob (JobName, LastRun) VALUES (@JobName, @LastRun)", new { JobName = jobName, LastRun = DateTime.UtcNow }, transaction);

            await connection.ExecuteAsync("""
                INSERT INTO BatchStep (StepName, JobName, StepIndex, NumberOfItemsProcessed)
                VALUES (@StepName, @JobName, @StepIndex, @NumberOfItemsProcessed)
                """,
                stepNames.Select(s => new
                {
                    StepName = s,
                    JobName = jobName,
                    StepIndex = 0,
                    NumberOfItemsProcessed = 0
                }), transaction);
        }

        await transaction.CommitAsync();
    }

    public async Task<long> InsertStepAsync(string stepName, long stepIndex)
    {
        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        using var transaction = await connection.BeginTransactionAsync();
        var result = await connection.QuerySingleAsync<long>("""
            INSERT INTO BatchStep (StepName, JobName, StepIndex, NumberOfItemsProcessed)
            VALUES (@StepName, @JobName, @StepIndex, @NumberOfItemsProcessed); SELECT SCOPE_IDENTITY();
            """,
            new
            {
                StepName = stepName,
                JobName = jobName,
                StepIndex = stepIndex,
                NumberOfItemsProcessed = 0
            }, transaction);
        await transaction.CommitAsync();
        return result;
    }

    public async Task UpdateStepAsync(long stepId, int numberOfItemsProcessed, bool error, bool skipped)
    {
        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        using var transaction = await connection.BeginTransactionAsync();
        await connection.ExecuteAsync("""
            Update BatchStep SET 
              NumberOfItemsProcessed = @NumberOfItemsProcessed, 
              Error = @Error, 
              Skipped = @Skipped 
            WHERE Id = @StepId
            """,
            new
            {
                NumberOfItemsProcessed = numberOfItemsProcessed,
                Error = error,
                Skipped = skipped,
                StepId = stepId
            }, transaction);
        await transaction.CommitAsync();
    }

    public async Task<int> GetExceptionCountAsync(SkipContext skipContext)
    {
        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        return await connection.QuerySingleAsync<int>("""
            SELECT COUNT(1) FROM BatchStepException WHERE StepName = @StepName AND JobName = @JobName
            """,
            new
            {
                StepName = skipContext.StepName,
                JobName = jobName
            });
    }

    public async Task<StepContext> GetStartIndexAsync(string stepName)
    {
        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        return await connection.QueryFirstAsync<StepContext>("""
            Select TOP 1 
              Id, 
              StepName, 
              JobName, 
              StepIndex, 
              NumberOfItemsProcessed 
            FROM BatchStep
            WHERE StepName = @StepName AND JobName = @JobName
            ORDER BY Id DESC
            """,
            new
            {
                StepName = stepName,
                JobName = jobName
            });
    }

    public async Task SaveExceptionInfoAsync(SkipContext skipContext, int currentCount)
    {
        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        using var transaction = await connection.BeginTransactionAsync();
        await connection.ExecuteAsync("""
            INSERT INTO BatchStepException (StepIndex, StepName, JobName, ExceptionMsg, ExceptionDetails)
            VALUES (@StepIndex, @StepName, @JobName, @ExceptionMsg, @ExceptionDetails)
            """,
            new
            {
                StepIndex = skipContext.StepIndex,
                StepName = skipContext.StepName,
                JobName = jobName,
                ExceptionMsg = skipContext.ExceptionMessage,
                ExceptionDetails = skipContext.ExceptionDetail
            }, transaction);
        await transaction.CommitAsync();
    }
}
