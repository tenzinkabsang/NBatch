using Moq;
using NBatch.Core;
using NBatch.Core.Repositories;
using NUnit.Framework;

namespace NBatch.Tests.Core;

/// <summary>
/// Retry-policy integration with the step pipeline: transient failures are
/// retried before the skip policy is consulted; exhaustion falls through to
/// the existing skip/fatal handling.
/// </summary>
[TestFixture]
internal sealed class StepRetryTests
{
    private Mock<IJobRepository> _jobRepo = null!;

    [SetUp]
    public void BeforeEach()
    {
        _jobRepo = new Mock<IJobRepository>();
        _jobRepo.Setup(r => r.GetStartIndexAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StepContext { StepName = "step1" });
    }

    [Test]
    public async Task Transient_read_failure_is_retried_and_no_skip_is_consumed()
    {
        var retryPolicy = RetryPolicy.For<TimeoutException>(maxAttempts: 3);
        var step = FakeStep<string, string>.Create("step1", _jobRepo.Object, chunkSize: 2, retryPolicy: retryPolicy);

        step.MockReader.SetupSequence(r => r.ReadAsync(0, 2, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("transient"))
            .ReturnsAsync(["a", "b"]);
        step.MockProcessor.Setup(p => p.ProcessAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string s, CancellationToken _) => s);

        var result = await step.ProcessAsync();

        Assert.That(result.Success, Is.True);
        Assert.That(result.ItemsWritten, Is.EqualTo(2));
        Assert.That(result.ItemsSkipped, Is.EqualTo(0));
        _jobRepo.Verify(r => r.SaveExceptionInfoAsync(It.IsAny<SkipContext>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    [Test]
    public async Task Transient_process_failure_is_retried_at_chunk_level_before_scan()
    {
        var retryPolicy = RetryPolicy.For<TimeoutException>(maxAttempts: 2);
        var step = FakeStep<string, string>.Create("step1", _jobRepo.Object, chunkSize: 2, retryPolicy: retryPolicy);

        step.MockReader.Setup(r => r.ReadAsync(0, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(["a", "b"]);
        step.MockProcessor.SetupSequence(p => p.ProcessAsync("a", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("transient"))
            .ReturnsAsync("a");
        step.MockProcessor.Setup(p => p.ProcessAsync("b", It.IsAny<CancellationToken>()))
            .ReturnsAsync("b");

        var result = await step.ProcessAsync();

        Assert.That(result.Success, Is.True);
        Assert.That(result.ItemsWritten, Is.EqualTo(2));
        // The retry succeeded at chunk level → one batch write of 2, no single-item scan writes.
        step.MockWriter.Verify(w => w.WriteAsync(It.Is<IEnumerable<string>>(items => items.Count() == 2), It.IsAny<CancellationToken>()), Times.Once());
        step.MockWriter.Verify(w => w.WriteAsync(It.Is<IEnumerable<string>>(items => items.Count() == 1), It.IsAny<CancellationToken>()), Times.Never());
    }

    [Test]
    public async Task Persistent_retryable_skippable_failure_is_retried_then_skipped_once()
    {
        // TimeoutException is both retryable and skippable. The bad item always fails:
        // it must be retried (chunk + scan attempts), then skipped exactly once.
        var retryPolicy = RetryPolicy.For<TimeoutException>(maxAttempts: 2);
        var skipPolicy = new SkipPolicy([typeof(TimeoutException)], skipLimit: 5);
        var step = FakeStep<string, string>.Create("step1", _jobRepo.Object, chunkSize: 2,
            skipPolicy: skipPolicy, retryPolicy: retryPolicy);

        int badItemCalls = 0;
        step.MockReader.Setup(r => r.ReadAsync(0, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(["good", "bad"]);
        step.MockProcessor.Setup(p => p.ProcessAsync("good", It.IsAny<CancellationToken>()))
            .ReturnsAsync("good");
        step.MockProcessor.Setup(p => p.ProcessAsync("bad", It.IsAny<CancellationToken>()))
            .Callback(() => badItemCalls++)
            .ThrowsAsync(new TimeoutException("always fails"));

        var result = await step.ProcessAsync();

        Assert.That(result.Success, Is.True);
        Assert.That(result.ItemsWritten, Is.EqualTo(1));
        Assert.That(result.ItemsSkipped, Is.EqualTo(1));
        // Chunk attempts: 2 (original + 1 retry). Scan attempts on the bad item: 2. Total 4.
        Assert.That(badItemCalls, Is.EqualTo(4));
        // Skip budget consumed exactly once, after retry exhaustion.
        _jobRepo.Verify(r => r.SaveExceptionInfoAsync(It.IsAny<SkipContext>(), It.IsAny<CancellationToken>()), Times.Once());
    }

    [Test]
    public void Persistent_retryable_nonskippable_failure_fails_step_after_max_attempts()
    {
        var retryPolicy = RetryPolicy.For<TimeoutException>(maxAttempts: 3);
        var step = FakeStep<string, string>.Create("step1", _jobRepo.Object, chunkSize: 1, retryPolicy: retryPolicy);

        int readCalls = 0;
        step.MockReader.Setup(r => r.ReadAsync(0, 1, It.IsAny<CancellationToken>()))
            .Callback(() => readCalls++)
            .ThrowsAsync(new TimeoutException("always fails"));

        Assert.ThrowsAsync<TimeoutException>(() => step.ProcessAsync());
        Assert.That(readCalls, Is.EqualTo(3), "chunk read should be attempted exactly maxAttempts times");
    }

    [Test]
    public void NonRetryable_exception_is_not_retried()
    {
        var retryPolicy = RetryPolicy.For<TimeoutException>(maxAttempts: 5);
        var step = FakeStep<string, string>.Create("step1", _jobRepo.Object, chunkSize: 1, retryPolicy: retryPolicy);

        int readCalls = 0;
        step.MockReader.Setup(r => r.ReadAsync(0, 1, It.IsAny<CancellationToken>()))
            .Callback(() => readCalls++)
            .ThrowsAsync(new InvalidOperationException("not retryable"));

        Assert.ThrowsAsync<InvalidOperationException>(() => step.ProcessAsync());
        Assert.That(readCalls, Is.EqualTo(1));
    }

    [Test]
    public async Task Scan_retries_each_item_independently()
    {
        // Chunk read always fails (skippable, not retryable for that type);
        // during the scan, item 0's single read is transient and succeeds on retry.
        var retryPolicy = RetryPolicy.For<TimeoutException>(maxAttempts: 2);
        var skipPolicy = new SkipPolicy([typeof(InvalidOperationException), typeof(TimeoutException)], skipLimit: 5);
        var step = FakeStep<string, string>.Create("step1", _jobRepo.Object, chunkSize: 2,
            skipPolicy: skipPolicy, retryPolicy: retryPolicy);

        step.MockReader.Setup(r => r.ReadAsync(0, 2, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("chunk read broken"));
        step.MockReader.SetupSequence(r => r.ReadAsync(0, 1, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("transient"))
            .ReturnsAsync(["item0"]);
        step.MockReader.Setup(r => r.ReadAsync(1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(["item1"]);
        step.MockProcessor.Setup(p => p.ProcessAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string s, CancellationToken _) => s);

        var result = await step.ProcessAsync();

        Assert.That(result.Success, Is.True);
        Assert.That(result.ItemsWritten, Is.EqualTo(2));
        Assert.That(result.ItemsSkipped, Is.EqualTo(0));
        step.MockReader.Verify(r => r.ReadAsync(0, 1, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Test]
    public void Retry_delay_is_interrupted_by_cancellation()
    {
        var retryPolicy = RetryPolicy.For<TimeoutException>(maxAttempts: 3, TimeSpan.FromSeconds(30));
        var step = FakeStep<string, string>.Create("step1", _jobRepo.Object, chunkSize: 1, retryPolicy: retryPolicy);

        using var cts = new CancellationTokenSource();
        step.MockReader.Setup(r => r.ReadAsync(0, 1, It.IsAny<CancellationToken>()))
            .Callback(() => cts.Cancel())
            .ThrowsAsync(new TimeoutException("transient"));

        var started = DateTime.UtcNow;
        Assert.ThrowsAsync<TaskCanceledException>(() => step.ProcessAsync(cts.Token));
        Assert.That(DateTime.UtcNow - started, Is.LessThan(TimeSpan.FromSeconds(10)),
            "cancellation must interrupt the retry delay, not wait it out");
    }
}
