using Moq;
using NBatch.Core;
using NBatch.Core.Repositories;
using NBatch.Readers.FileReader;
using NUnit.Framework;

namespace NBatch.Tests.Core;

[TestFixture]
class StepTests
{
    private Mock<IJobRepository> _jobRepo;

    [SetUp]
    public void BeforeEach()
    {
        _jobRepo = new Mock<IJobRepository>();
        _jobRepo.Setup(r => r.GetStartIndexAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StepContext());
    }

    [TestCase(1, 1)]
    [TestCase(10, 10)]
    public async Task WriterShouldBeCalledWithTheSpecifiedChunkSize(int chunkSize, int itemCount)
    {
        var step = FakeStep<string, string>.Create("step1", _jobRepo.Object, chunkSize);

        step.MockReader.Setup(r => r.ReadAsync(0, chunkSize, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Range(0, chunkSize).Select(s => "item read"));

        step.MockProcessor.Setup(p => p.ProcessAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("processed");

        var result = await step.ProcessAsync();

        step.MockWriter.Verify(w => w.WriteAsync(It.Is<IEnumerable<string>>(items => items.Count() == itemCount), It.IsAny<CancellationToken>()));
    }

    [Test]
    public async Task WhenSkippableExceptionsAreThrownItShouldProceedToTheNextChunk()
    {
        var skipPolicy = new SkipPolicy([typeof(FlatFileParseException)], skipLimit: 1);
        _jobRepo.Setup(r => r.GetStartIndexAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StepContext { StepName = "step1" });
        var step = FakeStep<string, string>.Create("step1", _jobRepo.Object, skipPolicy: skipPolicy);
        step.MockReader.Setup(r => r.ReadAsync(0, 1, It.IsAny<CancellationToken>())).ThrowsAsync(new FlatFileParseException());

        await step.ProcessAsync();

        _jobRepo.Verify(j => j.GetExceptionCountAsync(It.Is<SkipContext>(ctx => ctx.StepName == "step1"), It.IsAny<CancellationToken>()));
        _jobRepo.Verify(j => j.SaveExceptionInfoAsync(It.IsAny<SkipContext>(), It.IsAny<int>(), It.IsAny<CancellationToken>()));
    }

    [Test]
    public void StepIndexNotIncrementedWhenExceptionThrownAndSkipLimitReacher()
    {
        var skipPolicy = new SkipPolicy([typeof(Exception)], skipLimit: 1);
        var step = FakeStep<string, string>.Create("step1", _jobRepo.Object, skipPolicy: skipPolicy);

        // When it gets to the second iteration, we return 1 to indicate that we've already had one exception
        _jobRepo.Setup(r => r.GetExceptionCountAsync(It.Is<SkipContext>(ctx => ctx.StepIndex == 2), It.IsAny<CancellationToken>())).ReturnsAsync(1);

        step.MockReader.Setup(r => r.ReadAsync(It.IsAny<long>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(["line1"]);
        step.MockProcessor.Setup(p => p.ProcessAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(new Exception());

        // Should throw an excpetion on the second iteration since the skipLimit is reached.
        Assert.ThrowsAsync<Exception>(() => step.ProcessAsync());
    }

    [Test]
    public void IfNoSkipPolicySpecifiedThenThrowExceptionOnFirstError()
    {
        var step = FakeStep<string, string>.Create("step1", _jobRepo.Object, skipPolicy: null);
        step.MockReader.Setup(r => r.ReadAsync(0, 1, It.IsAny<CancellationToken>())).ThrowsAsync(new Exception());

        Assert.ThrowsAsync<Exception>(() => step.ProcessAsync());
        _jobRepo.Verify(r => r.GetExceptionCountAsync(It.IsAny<SkipContext>(), It.IsAny<CancellationToken>()), Times.Never());
    }
}
