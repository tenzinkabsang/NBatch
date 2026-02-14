using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NBatch.Core;
using NBatch.Core.Interfaces;
using NBatch.Core.Repositories;

namespace NBatch.Tests.Core;

internal class FakeStep<T, U>(string stepName,
Mock<IReader<T>> reader,
Mock<IProcessor<T, U>> processor,
Mock<IWriter<U>> writer,
IStepRepository stepRepository,
ILogger logger,
SkipPolicy? skipPolicy = null,
RetryPolicy? retryPolicy = null,
int chunkSize = 10) : Step<T, U>(stepName, reader.Object, processor.Object, writer.Object, stepRepository, logger, skipPolicy, retryPolicy, chunkSize)
{
    public Mock<IReader<T>> MockReader = reader;
    public Mock<IProcessor<T, U>> MockProcessor = processor;
    public Mock<IWriter<U>> MockWriter = writer;

    public static FakeStep<T, U> Create(string name, IStepRepository stepRepository, int chunkSize = 1, SkipPolicy? skipPolicy = null)
    {
        var reader = new Mock<IReader<T>>();
        var writer = new Mock<IWriter<U>>();
        var processor = new Mock<IProcessor<T, U>>();

        return new FakeStep<T, U>(name,
            reader,
            processor,
            writer,
            stepRepository,
            NullLogger.Instance,
            skipPolicy,
            chunkSize: chunkSize
            );
    }
}
