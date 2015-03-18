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

            _jobRepo.Verify(j => j.GetExceptionCount());
            _jobRepo.Verify(j => j.IncrementExceptionCount());
            _jobRepo.Verify(j => j.SaveExceptionDetails(It.IsAny<SkipContext>()));
        }

        [Test]
        public void IfSkipLimitIsNotSetThenThrowExceptionOnFirstError()
        {
            var step = FakeStep<string, string>.Create("step1");

            step.MockReader.Setup(r => r.Read(It.IsAny<int>(), It.IsAny<int>())).Throws<FlatFileParseException>();

            Assert.Throws<FlatFileParseException>(() => step.Process(0, _jobRepo.Object));
            _jobRepo.Verify(r => r.GetExceptionCount(), Times.Never());
        }
    }
}
