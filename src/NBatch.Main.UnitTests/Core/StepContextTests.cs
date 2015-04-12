using System.Runtime.InteropServices;
using NBatch.Main.Core;
using NUnit.Framework;

namespace NBatch.Main.UnitTests.Core
{
    [TestFixture]
    class StepContextTests
    {
        [Test]
        public void IfPreviousAttemptWasFailureThenRetryDuringRestart()
        {
            StepContext previous = new StepContext { NumberOfItemsProcessed = 0, StepIndex = 4};

            StepContext current = StepContext.InitialRun(previous, chunkSize: 2);

            Assert.That(current.StepIndex, Is.EqualTo(2));
            Assert.IsTrue(current.IsInitialRun);
        }

        [Test]
        public void IfPreviousAttemptWasTheFirstAttempThenRetryFirstItem()
        {
            StepContext previous = new StepContext { NumberOfItemsProcessed = 0, StepIndex = 0 };
            StepContext current = StepContext.InitialRun(previous, chunkSize: 2);

            Assert.That(current.StepIndex, Is.EqualTo(0));
        }

        [Test]
        public void IncrementTest()
        {
            StepContext previous = new StepContext { StepIndex = 4, ChunkSize = 2};

            StepContext current = StepContext.Increment(previous, 1, 1, false);
            Assert.That(current.StepIndex, Is.EqualTo(6));
        }

        [Test]
        public void HasNextTrueWhenInitialRun()
        {
            StepContext previous = new StepContext();
            StepContext current = StepContext.InitialRun(previous, chunkSize: 2);

            Assert.That(current.HasNext, Is.True);
        }

        [Test]
        public void HasNextTrueWhenItemSkipped()
        {
            StepContext ctx = new StepContext {Skip = true};
            Assert.That(ctx.HasNext, Is.True);
        }

        [TestCase(0, false)]
        [TestCase(1, true)]
        public void HasNextBasedOnNumberOfItemsReceived(int numOfItemsReceived, bool hasNext)
        {
            StepContext ctx = new StepContext {NumberOfItemsReceived = numOfItemsReceived, ChunkSize = 2};
            Assert.That(ctx.HasNext, Is.EqualTo(hasNext));
        }
    }
}
