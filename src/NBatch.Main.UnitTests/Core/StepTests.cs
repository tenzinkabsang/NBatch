using System;
using System.Collections.Generic;
using System.Linq;
using Moq;
using NBatch.Main.Core;
using NBatch.Main.Core.Repositories;
using NBatch.Main.Readers.FileReader;
using NUnit.Framework;

namespace NBatch.Main.UnitTests.Core
{
    [TestFixture]
    class StepTests
    {
        private Mock<IJobRepository> _jobRepo;

        [SetUp]
        public void BeforeEach()
        {
            _jobRepo = new Mock<IJobRepository>();
        }

        [TestCase(1, 1)]
        [TestCase(10, 10)]
        public void WriterShouldBeCalledWithTheSpecifiedChunkSize(int chunkSize, int itemCount)
        {
            var step1 = FakeStep<string, string>.Create("step1");
            step1.WithChunkSize(chunkSize);

            step1.MockReader.Setup(r => r.Read(0, chunkSize)).Returns(Enumerable.Range(0, chunkSize).Select(s => "item read"));
            step1.MockProcessor.Setup(p => p.Process(It.IsAny<string>())).Returns("processed");

            step1.Process(new StepContext(), _jobRepo.Object);

            step1.MockWriter.Verify(w => w.Write(It.Is<IEnumerable<string>>(items => items.Count() == itemCount)));
        }

        [Test]
        public void SkippableExceptionsAreSkipped()
        {
            var step = FakeStep<string, string>.Create("step1");
            step.SkipLimit(1)
                .SkippableExceptions(typeof (FlatFileParseException), typeof (Exception));

            step.MockReader.Setup(r => r.Read(0, 1)).Throws<FlatFileParseException>();

            var stepContext = new StepContext {StepName = "step1"};
            step.Process(stepContext, _jobRepo.Object);

            _jobRepo.Verify(j => j.GetExceptionCount(It.Is<SkipContext>(ctx => ctx.StepName == "step1")));
            _jobRepo.Verify(j => j.SaveExceptionInfo(It.IsAny<SkipContext>(), It.IsAny<int>()));
        }

        [Test]
        public void StepIndexNotIncrementedWhenExceptionThrownAndSkipLimitReached()
        {
            var step = FakeStep<string, string>.Create("step1");
            step.SkipLimit(1).SkippableExceptions(typeof (Exception));

            _jobRepo.Setup(r => r.GetExceptionCount(It.Is<SkipContext>(ctx => ctx.StepIndex == 2))).Returns(1);

            step.MockReader.Setup(r => r.Read(It.IsAny<long>(), It.IsAny<int>())).Returns(new[] {"line1"});
            step.MockProcessor.Setup(p => p.Process(It.IsAny<string>())).Throws<Exception>();

            Assert.Throws<Exception>(() => step.Process(new StepContext(), _jobRepo.Object));
        }
        
        [Test]
        public void WhenSkipLimitIsReachedThrowException()
        {
            // Using an in-memory job repository in order to increment exception count
            var inMemoryJobRepo = new InMemoryJobRepository();

            var step = FakeStep<string, string>.Create("step1");
            step.WithChunkSize(10);
            step.SkipLimit(5)
                .SkippableExceptions(typeof (Exception));

            step.MockReader.Setup(r => r.Read(It.IsAny<long>(), It.IsAny<int>())).Throws<FlatFileParseException>();

            Assert.Throws<FlatFileParseException>(() => step.Process(new StepContext(), inMemoryJobRepo));
        }

        [Test]
        public void IfSkipLimitIsNotSetThenThrowExceptionOnFirstError()
        {
            var step = FakeStep<string, string>.Create("step1");

            step.MockReader.Setup(r => r.Read(It.IsAny<long>(), It.IsAny<int>())).Throws<FlatFileParseException>();

            Assert.Throws<FlatFileParseException>(() => step.Process(new StepContext(), _jobRepo.Object));
            _jobRepo.Verify(r => r.GetExceptionCount(It.IsAny<SkipContext>()), Times.Never());
        }
    }
}
