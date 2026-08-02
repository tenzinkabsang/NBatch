using Moq;
using NBatch.Core;
using NBatch.Core.Repositories;
using NBatch.Readers.FileReader;
using NUnit.Framework;

namespace NBatch.Tests.Core;

[TestFixture]
internal class StepTests
{
    private Mock<IJobRepository> _jobRepo = null!;

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
        _jobRepo.Verify(j => j.SaveExceptionInfoAsync(It.IsAny<SkipContext>(), It.IsAny<CancellationToken>()));
    }

    [Test]
    public void Step_fails_when_skip_limit_reached_during_scan()
    {
        var skipPolicy = new SkipPolicy([typeof(Exception)], skipLimit: 1);
        var step = FakeStep<string, string>.Create("step1", _jobRepo.Object, skipPolicy: skipPolicy);

        // SkipContext.StepIndex is the per-ITEM index. When the item at index 2 fails,
        // report that one exception was already recorded — the skip limit is reached.
        _jobRepo.Setup(r => r.GetExceptionCountAsync(It.Is<SkipContext>(ctx => ctx.StepIndex == 2), It.IsAny<CancellationToken>())).ReturnsAsync(1);

        step.MockReader.Setup(r => r.ReadAsync(It.IsAny<long>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(["line1"]);
        step.MockProcessor.Setup(p => p.ProcessAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(new Exception());

        // Items at index 0 and 1 are skipped; the item at index 2 exhausts the limit → the step fails.
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

    #region Item-level scan mode

    [Test]
    public async Task Scan_after_process_failure_writes_good_items_individually()
    {
        // Chunk of 3 where item "bad" always fails: the chunk-level attempt fails,
        // the scan re-processes item-by-item, and only "bad" is skipped.
        var skipPolicy = new SkipPolicy([typeof(FormatException)], skipLimit: 5);
        var step = FakeStep<string, string>.Create("step1", _jobRepo.Object, chunkSize: 3, skipPolicy: skipPolicy);

        step.MockReader.Setup(r => r.ReadAsync(0, 3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(["good1", "bad", "good2"]);
        step.MockProcessor.Setup(p => p.ProcessAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string s, CancellationToken _) => s);
        step.MockProcessor.Setup(p => p.ProcessAsync("bad", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new FormatException("bad item"));

        var result = await step.ProcessAsync();

        Assert.That(result.Success, Is.True);
        Assert.That(result.ItemsRead, Is.EqualTo(3));
        Assert.That(result.ItemsProcessed, Is.EqualTo(2));
        Assert.That(result.ErrorsSkipped, Is.EqualTo(1));

        // The failed batch write never happened; the good items were written as single-item batches.
        step.MockWriter.Verify(w => w.WriteAsync(It.Is<IEnumerable<string>>(items => items.Count() == 1), It.IsAny<CancellationToken>()), Times.Exactly(2));
        step.MockWriter.Verify(w => w.WriteAsync(It.Is<IEnumerable<string>>(items => items.Count() == 3), It.IsAny<CancellationToken>()), Times.Never());
    }

    [Test]
    public async Task Scan_after_write_failure_rewrites_items_individually()
    {
        // The batch write fails, but single-item writes succeed → all items written, none skipped.
        var skipPolicy = new SkipPolicy([typeof(TimeoutException)], skipLimit: 5);
        var step = FakeStep<string, string>.Create("step1", _jobRepo.Object, chunkSize: 3, skipPolicy: skipPolicy);

        step.MockReader.Setup(r => r.ReadAsync(0, 3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(["a", "b", "c"]);
        step.MockProcessor.Setup(p => p.ProcessAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string s, CancellationToken _) => s);
        step.MockWriter.Setup(w => w.WriteAsync(It.Is<IEnumerable<string>>(items => items.Count() > 1), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("batch write failed"));

        var result = await step.ProcessAsync();

        Assert.That(result.Success, Is.True);
        Assert.That(result.ItemsProcessed, Is.EqualTo(3));
        Assert.That(result.ErrorsSkipped, Is.EqualTo(0));
        step.MockWriter.Verify(w => w.WriteAsync(It.Is<IEnumerable<string>>(items => items.Count() == 1), It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    [Test]
    public async Task Scan_after_read_failure_rereads_items_one_at_a_time()
    {
        // The chunk read fails; single-item re-reads succeed except position 1.
        var skipPolicy = new SkipPolicy([typeof(FlatFileParseException)], skipLimit: 5);
        var step = FakeStep<string, string>.Create("step1", _jobRepo.Object, chunkSize: 3, skipPolicy: skipPolicy);

        step.MockReader.Setup(r => r.ReadAsync(0, 3, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new FlatFileParseException());
        step.MockReader.Setup(r => r.ReadAsync(0, 1, It.IsAny<CancellationToken>())).ReturnsAsync(["item0"]);
        step.MockReader.Setup(r => r.ReadAsync(1, 1, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new FlatFileParseException());
        step.MockReader.Setup(r => r.ReadAsync(2, 1, It.IsAny<CancellationToken>())).ReturnsAsync(["item2"]);
        step.MockProcessor.Setup(p => p.ProcessAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string s, CancellationToken _) => s);

        var result = await step.ProcessAsync();

        Assert.That(result.Success, Is.True);
        Assert.That(result.ItemsRead, Is.EqualTo(2));
        Assert.That(result.ItemsProcessed, Is.EqualTo(2));
        Assert.That(result.ErrorsSkipped, Is.EqualTo(1));

        step.MockReader.Verify(r => r.ReadAsync(0, 1, It.IsAny<CancellationToken>()), Times.Once());
        step.MockReader.Verify(r => r.ReadAsync(1, 1, It.IsAny<CancellationToken>()), Times.Once());
        step.MockReader.Verify(r => r.ReadAsync(2, 1, It.IsAny<CancellationToken>()), Times.Once());
    }

    [Test]
    public async Task Scan_stops_at_end_of_data()
    {
        // The chunk read fails; the scan reads one good item, then hits end-of-data.
        var skipPolicy = new SkipPolicy([typeof(FlatFileParseException)], skipLimit: 5);
        var step = FakeStep<string, string>.Create("step1", _jobRepo.Object, chunkSize: 3, skipPolicy: skipPolicy);

        step.MockReader.Setup(r => r.ReadAsync(0, 3, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new FlatFileParseException());
        step.MockReader.Setup(r => r.ReadAsync(0, 1, It.IsAny<CancellationToken>())).ReturnsAsync(["only-item"]);
        // reads at 1 and 2 return empty (Moq default)
        step.MockProcessor.Setup(p => p.ProcessAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string s, CancellationToken _) => s);

        var result = await step.ProcessAsync();

        Assert.That(result.Success, Is.True);
        Assert.That(result.ItemsRead, Is.EqualTo(1));
        Assert.That(result.ItemsProcessed, Is.EqualTo(1));
        Assert.That(result.ErrorsSkipped, Is.EqualTo(0));
        // The scan stopped at position 1 (end of data) and never probed position 2.
        step.MockReader.Verify(r => r.ReadAsync(2, 1, It.IsAny<CancellationToken>()), Times.Never());
    }

    [Test]
    public void Scan_rethrows_when_limit_exhausted_midway_and_records_error()
    {
        // Two bad items in one chunk with a limit of 1: the first is skipped, the
        // second exhausts the limit → the step fails, and the chunk row records the error.
        var skipPolicy = new SkipPolicy([typeof(FormatException)], skipLimit: 1);
        var step = FakeStep<string, string>.Create("step1", _jobRepo.Object, chunkSize: 3, skipPolicy: skipPolicy);

        int savedExceptions = 0;
        _jobRepo.Setup(r => r.GetExceptionCountAsync(It.IsAny<SkipContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => savedExceptions);
        _jobRepo.Setup(r => r.SaveExceptionInfoAsync(It.IsAny<SkipContext>(), It.IsAny<CancellationToken>()))
            .Callback(() => savedExceptions++)
            .Returns(Task.CompletedTask);

        step.MockReader.Setup(r => r.ReadAsync(0, 3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(["good", "bad1", "bad2"]);
        step.MockProcessor.Setup(p => p.ProcessAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string s, CancellationToken _) => s);
        step.MockProcessor.Setup(p => p.ProcessAsync(It.IsIn("bad1", "bad2"), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new FormatException("bad item"));

        Assert.ThrowsAsync<FormatException>(() => step.ProcessAsync());

        // Partial progress recorded with the error flag so restart backs up one chunk.
        _jobRepo.Verify(r => r.UpdateStepAsync(It.IsAny<long>(), 1, true, true, It.IsAny<CancellationToken>()), Times.Once());
    }

    [Test]
    public void Non_matching_exception_fails_without_scanning()
    {
        var skipPolicy = new SkipPolicy([typeof(TimeoutException)], skipLimit: 5);
        var step = FakeStep<string, string>.Create("step1", _jobRepo.Object, chunkSize: 3, skipPolicy: skipPolicy);

        step.MockReader.Setup(r => r.ReadAsync(0, 3, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("not skippable"));

        Assert.ThrowsAsync<InvalidOperationException>(() => step.ProcessAsync());

        // Fail-fast: no single-item re-reads were attempted.
        step.MockReader.Verify(r => r.ReadAsync(It.IsAny<long>(), 1, It.IsAny<CancellationToken>()), Times.Never());
        _jobRepo.Verify(r => r.GetExceptionCountAsync(It.IsAny<SkipContext>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    [Test]
    public async Task Processor_is_reinvoked_for_items_of_a_failed_chunk()
    {
        // Pins the documented idempotency contract: a failed chunk's items are
        // re-processed during the scan, so the processor runs chunk + per-item times.
        var skipPolicy = new SkipPolicy([typeof(FormatException)], skipLimit: 5);
        var step = FakeStep<string, string>.Create("step1", _jobRepo.Object, chunkSize: 2, skipPolicy: skipPolicy);

        int processorCalls = 0;
        step.MockReader.Setup(r => r.ReadAsync(0, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(["a", "bad"]);
        step.MockProcessor.Setup(p => p.ProcessAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback(() => processorCalls++)
            .ReturnsAsync((string s, CancellationToken _) => s);
        step.MockProcessor.Setup(p => p.ProcessAsync("bad", It.IsAny<CancellationToken>()))
            .Callback(() => processorCalls++)
            .ThrowsAsync(new FormatException("bad item"));

        await step.ProcessAsync();

        // Chunk attempt: "a" then "bad" (throws) = 2 calls. Scan: "a" + "bad" = 2 more.
        Assert.That(processorCalls, Is.EqualTo(4));
    }

    #endregion
}
