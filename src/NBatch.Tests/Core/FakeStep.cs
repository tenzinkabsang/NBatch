using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NBatch.Core;
using NBatch.Core.Interfaces;
using NBatch.Core.Repositories;

namespace NBatch.Tests.Core;

internal class FakeStep<T, U> : Step<T, U>
{
    public Mock<IReader<T>> MockReader;
    public Mock<IProcessor<T, U>> MockProcessor;
    public Mock<IWriter<U>> MockWriter;

    private FakeStep(string stepName,
        Mock<IReader<T>> reader,
        Mock<IProcessor<T, U>> processor,
        Mock<IWriter<U>> writer,
        IStepRepository stepRepository,
        ILogger logger,
        SkipPolicy? skipPolicy = null,
        int chunkSize = 10)
        : base(stepName, reader.Object, processor.Object, writer.Object, stepRepository, logger, skipPolicy, chunkSize)
    {
        MockReader = reader;
        MockProcessor = processor;
        MockWriter = writer;
    }

    public static FakeStep<T, U> Create(string name, IStepRepository stepRepository, int chunkSize = 1, SkipPolicy? skipPolicy = null)
    {
        return new FakeStep<T, U>(name,
            new Mock<IReader<T>>(),
            new Mock<IProcessor<T, U>>(),
            new Mock<IWriter<U>>(),
            stepRepository,
            NullLogger.Instance,
            skipPolicy,
            chunkSize: chunkSize);
    }
}
