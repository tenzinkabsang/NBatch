using NBatch.Core.Repositories;
using NUnit.Framework;

namespace NBatch.Tests.Core;

/// <summary>
/// Resume-position semantics of <see cref="InMemoryJobRepository"/>: a step row is
/// presumed failed from insert until its outcome is recorded, and the resume index
/// is always the latest committed (non-error) position.
/// </summary>
[TestFixture]
internal sealed class JobRepositoryResumeTests
{
    private InMemoryJobRepository _repo = null!;

    [SetUp]
    public async Task BeforeEach()
    {
        _repo = new InMemoryJobRepository("test-job");
        await _repo.CreateJobRecordAsync(["step1"]);
    }

    [Test]
    public async Task Dangling_insert_resumes_at_last_committed_index()
    {
        // Chunk [0, 5) started but never committed (simulated crash between
        // InsertStepAsync and UpdateStepAsync).
        await _repo.InsertStepAsync("step1", 5);

        var ctx = await _repo.GetStartIndexAsync("step1");

        Assert.That(ctx.Error, Is.True, "an uncommitted chunk must read as failed");
        Assert.That(ctx.StepIndex, Is.EqualTo(0), "resume must re-process the interrupted chunk");
    }

    [Test]
    public async Task Committed_chunk_resumes_at_its_recorded_index()
    {
        long id = await _repo.InsertStepAsync("step1", 5);
        await _repo.UpdateStepAsync(id, 5, error: false, skipped: false);

        var ctx = await _repo.GetStartIndexAsync("step1");

        Assert.That(ctx.Error, Is.False);
        Assert.That(ctx.StepIndex, Is.EqualTo(5));
    }

    [Test]
    public async Task Failed_chunk_resumes_at_the_previous_committed_index()
    {
        long first = await _repo.InsertStepAsync("step1", 5);
        await _repo.UpdateStepAsync(first, 5, error: false, skipped: false);

        long second = await _repo.InsertStepAsync("step1", 10);
        await _repo.UpdateStepAsync(second, 0, error: true, skipped: false);

        var ctx = await _repo.GetStartIndexAsync("step1");

        Assert.That(ctx.Error, Is.True);
        Assert.That(ctx.StepIndex, Is.EqualTo(5), "resume at the failed chunk's start, not its end");
    }

    [Test]
    public async Task Repeated_failures_still_resume_at_the_last_committed_index()
    {
        long first = await _repo.InsertStepAsync("step1", 5);
        await _repo.UpdateStepAsync(first, 5, error: false, skipped: false);

        // Two failed attempts at the same chunk pile up error rows at index 10.
        long fail1 = await _repo.InsertStepAsync("step1", 10);
        await _repo.UpdateStepAsync(fail1, 0, error: true, skipped: false);
        long fail2 = await _repo.InsertStepAsync("step1", 10);
        await _repo.UpdateStepAsync(fail2, 0, error: true, skipped: false);

        var ctx = await _repo.GetStartIndexAsync("step1");

        Assert.That(ctx.StepIndex, Is.EqualTo(5));
    }

    [Test]
    public async Task Failure_on_the_first_chunk_resumes_at_zero()
    {
        long id = await _repo.InsertStepAsync("step1", 5);
        await _repo.UpdateStepAsync(id, 0, error: true, skipped: false);

        var ctx = await _repo.GetStartIndexAsync("step1");

        Assert.That(ctx.StepIndex, Is.EqualTo(0));
    }
}
