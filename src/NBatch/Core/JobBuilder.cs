using NBatch.Core.Interfaces;
using NBatch.Core.Repositories;

namespace NBatch.Core;

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

    public JobBuilder AddStep<TInput, TOutput>(
        string stepName,
        IReader<TInput> reader,
        IWriter<TOutput> writer,
        IProcessor<TInput, TOutput>? processor = null,
        SkipPolicy? skipPolicy = null,
        int chunkSize = 10)
    {
        IStep s = new Step<TInput, TOutput>(stepName, reader, processor, writer, skipPolicy, chunkSize);
        Ensure.UniqueStepNames(_steps.Keys, s);

        _steps.Add(s.Name, s);
        return this;
    }

    public Job Build() => new(_jobName, _steps, _jobRepository);
}
