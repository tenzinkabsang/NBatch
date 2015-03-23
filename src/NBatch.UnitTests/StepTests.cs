using Moq;
using NBatch.Core;
using NBatch.Core.Reader.FileReader;
using NBatch.Core.Repositories;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NBatch.UnitTests
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

            step1.Process(0, _jobRepo.Object);

            step1.MockWriter.Verify(w => w.Write(It.Is<IEnumerable<string>>(items => items.Count() == itemCount)));
        }

        [Test]
        public void SkippableExceptionsShouldBeSkippedUntilSkipLimitIsReached()
        {
            var step = FakeStep<string, string>.Create("step1");
            step.SkipLimit(1)
                .SkippableExceptions(typeof (FlatFileParseException), typeof (Exception));

            step.MockReader.Setup(r => r.Read(0, 1)).Throws<FlatFileParseException>();

            step.Process(0, _jobRepo.Object);

            _jobRepo.Verify(j => j.GetExceptionCount(It.Is<SkipContext>(ctx => ctx.StepName == "step1")));
            _jobRepo.Verify(j => j.SaveExceptionInfo(It.IsAny<SkipContext>(), It.IsAny<int>()));
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

            Assert.Throws<FlatFileParseException>(() => step.Process(0, inMemoryJobRepo));
        }

        [Test]
        public void IfSkipLimitIsNotSetThenThrowExceptionOnFirstError()
        {
            var step = FakeStep<string, string>.Create("step1");

            step.MockReader.Setup(r => r.Read(It.IsAny<long>(), It.IsAny<int>())).Throws<FlatFileParseException>();

            Assert.Throws<FlatFileParseException>(() => step.Process(0, _jobRepo.Object));
            _jobRepo.Verify(r => r.GetExceptionCount(It.IsAny<SkipContext>()), Times.Never());
        }
    }
}
