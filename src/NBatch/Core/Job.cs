using NBatch.Core.Interfaces;
using NBatch.Core.Repositories;

namespace NBatch.Core;

public sealed class Job
{
    private readonly string _jobName;
    private readonly IJobRepository _jobRepository;
    private readonly IDictionary<string, IStep> _steps;
    
    internal Job(string jobName, IDictionary<string, IStep> steps, IJobRepository jobRepository)
    {
        _jobName = jobName;
        _steps = steps;
        _jobRepository = jobRepository;
    }

    public async Task<JobResult> StartAsync()
    {
        await _jobRepository.CreateJobRecordAsync(_steps.Keys);
        bool success = true;
        foreach (var step in _steps)
        {
            StepContext stepContext = await _jobRepository.GetStartIndexAsync(step.Key);
            success &= (await step.Value.ProcessAsync(stepContext, _jobRepository)).Success; 
        }
        return new JobResult(_jobName, success);
    }

    public static JobBuilder CreateBuilder(string jobName, string connString) 
        => new(jobName, connString);
}

public sealed class JobBuilder
{
    private readonly string _jobName;
    private readonly IJobRepository _jobRepository;
    private readonly Dictionary<string, IStep> _steps;

    internal JobBuilder(string jobName, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(jobName);
        ArgumentNullException.ThrowIfNull(connectionString);
        _steps = [];
        _jobName = jobName;
        _jobRepository = new SqlJobRepository(jobName, connectionString);
    }

    public JobBuilder AddStep(IStep step)
    {
        Ensure.UniqueStepNames(_steps.Keys, step);
        _steps.Add(step.Name, step);
        return this;
    }

    public Job Build() => new(_jobName, _steps, _jobRepository);
}

public class Test
{
    public async Task Run()
    {
        var step = new Step<object, object>("name",
            reader: null,
            processor: null,
            writer: null,
            skipPolicy: null,
            chunkSize: 100
            );


        var jobBuilder = Job.CreateBuilder("jobName", "connectionString");
        jobBuilder.AddStep(step);
        jobBuilder.AddStep(step);
        jobBuilder.AddStep(step);

        var job = jobBuilder.Build();
        await job.StartAsync();


        //jobBuilder.AddStep("step1", s =>
        //{
        //    var reader = new object();
        //    s.AddReader(reader);
        //    s.AddProcessor();
        //    s.AddWriter();
        //    s.ConfigureSkipPolicy([typeof(ArgumentException)], 10);
        //    s.BatchSize(10);
        //});

    }
}
