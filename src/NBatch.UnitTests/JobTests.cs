using Moq;
using NBatch.Core;
using NBatch.Core.Repositories;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;

namespace NBatch.UnitTests
{
    [TestFixture]
    class JobTests
    {
        [Test]
        public void JobCanContainMultipleSteps()
        {
            var step1 = FakeStep<string, string>.Create("step1");
            var step2 = FakeStep<string, string>.Create("step2");

            step1.MockReader.Setup(r => r.Read(0, 1)).Returns(new[] { "STEP1: item read" });
            step1.MockProcessor.Setup(p => p.Process(It.IsAny<string>())).Returns("STEP1: item processed");

            step2.MockReader.Setup(r => r.Read(0, 1)).Returns(new[] { "STEP2: item read" });
            step2.MockProcessor.Setup(p => p.Process(It.IsAny<string>())).Returns("STEP2: item processed");

            var jobRepo = new Mock<IJobRepository>();

            new Job(jobRepo.Object)
                .AddStep(step1)
                .AddStep(step2)
                .Start();

            jobRepo.Verify(j => j.GetStartIndex(), Times.Exactly(2));
            VerifyMocks(step1, "STEP1: item read", "STEP1: item processed");
            VerifyMocks(step2, "STEP2: item read", "STEP2: item processed");
        }

        private void VerifyMocks(FakeStep<string, string> step, string readString, string processedString)
        {
            step.MockReader.Verify(reader => reader.Read(0, 1), Times.Once);
            step.MockProcessor.Verify(p => p.Process(readString));
            step.MockWriter.Verify(writer => writer.Write(
                It.Is<IEnumerable<string>>(items => items.Count() == 1 && items.Contains(processedString))));

        }
    }
}
