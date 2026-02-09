using NBatch.Core.Exceptions;
using NBatch.Core.Interfaces;
using NBatch.Core.Repositories;

namespace NBatch.Core;

public sealed class JobBuilder
{
    private readonly string _jobName;
    private readonly IJobRepository _jobRepository;
    private readonly Dictionary<string, IStep> _steps = [];

    internal JobBuilder(string jobName, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(jobName);
        ArgumentNullException.ThrowIfNull(connectionString);
        _jobName = jobName;
        _jobRepository = new SqlJobRepository(jobName, connectionString);
    }

    public JobBuilder AddStep<TInput, TOutput>(
        string stepName,
        IReader<TInput> reader,
        IWriter<TOutput> writer,
        IProcessor<TInput, TOutput>? processor = null,
        SkipPolicy? skipPolicy = null,
        int chunkSize = 10)
    {
        ArgumentNullException.ThrowIfNull(stepName);
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(writer);

        if (_steps.ContainsKey(stepName))
            throw new DuplicateStepNameException();

        var step = new Step<TInput, TOutput>(stepName, reader, processor, writer, skipPolicy, chunkSize);
        _steps.Add(step.Name, step);
        return this;
    }

    public Job Build() => new(_jobName, _steps, _jobRepository);
}
