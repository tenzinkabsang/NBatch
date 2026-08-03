using NBatch.Core;
using NUnit.Framework;

namespace NBatch.Tests.Core;

[TestFixture]
internal class StepContextTests
{
    [Test]
    public void InitialRun_preserves_repository_resolved_resume_index()
    {
        // The repository resolves the resume index (last committed position);
        // InitialRun must use it verbatim — no back-up arithmetic.
        var previous = new StepContext { NumberOfItemsProcessed = 0, StepIndex = 4, Error = true };

        var current = StepContext.InitialRun(previous, chunkSize: 2);

        Assert.That(current.StepIndex, Is.EqualTo(4));
        Assert.That(current.FirstIteration, Is.True);
    }

    [Test]
    public void IfPreviousAttemptWasTheFirstAttemptThenRetryFirstItem()
    {
        var previous = new StepContext { NumberOfItemsProcessed = 0, StepIndex = 0 };
        var current = StepContext.InitialRun(previous, chunkSize: 2);
        Assert.That(current.StepIndex, Is.EqualTo(0));
    }

    [Test]
    public void IncrementTest()
    {
        var previous = new StepContext { StepIndex = 4, ChunkSize = 2 };
        var current = StepContext.Increment(previous,
            itemsReceived: 1,
            itemsProcessed: 1,
            itemsSkipped: 0);
        Assert.That(current.StepIndex, Is.EqualTo(6));
    }

    [Test]
    public void HasNextIsTrueWhenInitialRun()
    {
        var previous = new StepContext();
        var current = StepContext.InitialRun(previous, chunkSize: 2);
        Assert.That(current.HasNext, Is.True);
    }

    [Test]
    public void HasNextIsTrueWhenItemSkipped()
    {
        var ctx = new StepContext { Skip = true };
        Assert.That(ctx.HasNext, Is.True);
    }

    [TestCase(0, false)]
    [TestCase(1, true)]
    public void HasNextBasedOnNumberOfItemsReceived(int numOfItemsReceived, bool hasNext)
    {
        StepContext ctx = new StepContext { NumberOfItemsReceived = numOfItemsReceived, ChunkSize = 2 };
        Assert.That(ctx.HasNext, Is.EqualTo(hasNext));
    }

    #region InitialRun uses the repository-resolved index verbatim

    [TestCase(4, false)]
    [TestCase(4, true)]
    [TestCase(0, true)]
    public void InitialRun_never_alters_the_resume_index(long resumeIndex, bool error)
    {
        // Resume-position resolution lives in the repositories (latest committed
        // row); InitialRun uses the resolved index for every outcome.
        var previous = new StepContext { StepIndex = resumeIndex, NumberOfItemsProcessed = 0, Error = error };

        var current = StepContext.InitialRun(previous, chunkSize: 2);

        Assert.That(current.StepIndex, Is.EqualTo(resumeIndex));
    }

    #endregion

    #region Increment preserves StepName

    [Test]
    public void Increment_preserves_step_name()
    {
        var previous = new StepContext { StepName = "import", StepIndex = 0, ChunkSize = 10 };

        var current = StepContext.Increment(previous, itemsReceived: 5, itemsProcessed: 5, itemsSkipped: 0);

        Assert.That(current.StepName, Is.EqualTo("import"));
    }

    [Test]
    public void Increment_sets_skip_flag_and_item_count()
    {
        var previous = new StepContext { StepIndex = 0, ChunkSize = 5 };

        var current = StepContext.Increment(previous, itemsReceived: 5, itemsProcessed: 3, itemsSkipped: 2);

        Assert.That(current.Skip, Is.True);
        Assert.That(current.NumberOfItemsSkipped, Is.EqualTo(2));
        Assert.That(current.HasNext, Is.True);
    }

    #endregion
}
