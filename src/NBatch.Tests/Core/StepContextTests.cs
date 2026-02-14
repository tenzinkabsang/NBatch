using NBatch.Core;
using NUnit.Framework;

namespace NBatch.Tests.Core;

[TestFixture]
class StepContextTests
{
    [Test]
    public void IfPreviousAttemptFailedThenItShouldRetryDuringRestart()
    {
        var previous = new StepContext { NumberOfItemsProcessed = 0, StepIndex = 4 };

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
}
