using NBatch.Core;
using NUnit.Framework;

namespace NBatch.Tests.Core;

[TestFixture]
internal class StepContextTests
{
    [Test]
    public void IfPreviousAttemptFailedThenItShouldRetryDuringRestart()
    {
        var previous = new StepContext { NumberOfItemsProcessed = 0, StepIndex = 4, Error = true };

        var current = StepContext.InitialRun(previous, chunkSize: 2);

        Assert.That(current.StepIndex, Is.EqualTo(2));
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
            skipped: false);
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

    #region BackUpIfPreviousFailed — Error vs Skip distinction

    [Test]
    public void Skipped_chunk_does_not_trigger_backup()
    {
        // A skipped chunk has NumberOfItemsProcessed=0 but Error=false.
        // BackUpIfPreviousFailed should NOT back up — the skip was intentional.
        var previous = new StepContext { StepIndex = 4, NumberOfItemsProcessed = 0, Error = false };

        var current = StepContext.InitialRun(previous, chunkSize: 2);

        Assert.That(current.StepIndex, Is.EqualTo(4));
    }

    [Test]
    public void Failed_chunk_with_error_flag_backs_up()
    {
        // A truly failed chunk has Error=true.
        var previous = new StepContext { StepIndex = 6, NumberOfItemsProcessed = 0, Error = true };

        var current = StepContext.InitialRun(previous, chunkSize: 2);

        Assert.That(current.StepIndex, Is.EqualTo(4));
    }

    [Test]
    public void Successful_chunk_does_not_back_up()
    {
        var previous = new StepContext { StepIndex = 4, NumberOfItemsProcessed = 5, Error = false };

        var current = StepContext.InitialRun(previous, chunkSize: 2);

        Assert.That(current.StepIndex, Is.EqualTo(4));
    }

    [Test]
    public void Failed_at_index_zero_stays_at_zero()
    {
        // Can't back up below 0 even when Error=true.
        var previous = new StepContext { StepIndex = 0, NumberOfItemsProcessed = 0, Error = true };

        var current = StepContext.InitialRun(previous, chunkSize: 2);

        Assert.That(current.StepIndex, Is.EqualTo(0));
    }

    #endregion

    #region Increment preserves StepName

    [Test]
    public void Increment_preserves_step_name()
    {
        var previous = new StepContext { StepName = "import", StepIndex = 0, ChunkSize = 10 };

        var current = StepContext.Increment(previous, itemsReceived: 5, itemsProcessed: 5, skipped: false);

        Assert.That(current.StepName, Is.EqualTo("import"));
    }

    [Test]
    public void Increment_sets_skip_flag()
    {
        var previous = new StepContext { StepIndex = 0, ChunkSize = 5 };

        var current = StepContext.Increment(previous, itemsReceived: 5, itemsProcessed: 0, skipped: true);

        Assert.That(current.Skip, Is.True);
        Assert.That(current.HasNext, Is.True);
    }

    #endregion
}
