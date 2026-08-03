using NBatch.Core;
using NBatch.Core.Exceptions;
using NBatch.Core.Interfaces;
using NUnit.Framework;

namespace NBatch.Tests.Core;

[TestFixture]
internal sealed class JobResultTests
{
    [Test]
    public void EnsureSuccess_returns_same_instance_when_successful()
    {
        var result = new JobResult("ok-job", Success: true, [new StepResult("s1", true)]);

        Assert.That(result.EnsureSuccess(), Is.SameAs(result));
    }

    [Test]
    public void EnsureSuccess_throws_JobFailedException_when_failed()
    {
        var result = new JobResult("bad-job", Success: false, [new StepResult("s1", false)]);

        Assert.Throws<JobFailedException>(() => result.EnsureSuccess());
    }

    [Test]
    public void JobFailedException_message_names_first_failed_step()
    {
        var result = new JobResult("etl", Success: false,
            [new StepResult("extract", true), new StepResult("transform", false)]);

        var ex = Assert.Throws<JobFailedException>(() => result.EnsureSuccess());

        Assert.That(ex!.Message, Does.Contain("etl"));
        Assert.That(ex.Message, Does.Contain("transform"));
    }

    [Test]
    public void JobFailedException_carries_the_result()
    {
        var result = new JobResult("bad-job", Success: false, [new StepResult("s1", false)]);

        var ex = Assert.Throws<JobFailedException>(() => result.EnsureSuccess());

        Assert.That(ex!.Result, Is.SameAs(result));
    }

    [Test]
    public void JobFailedException_message_mentions_cancellation_when_cancelled()
    {
        var result = new JobResult("slow-job", Success: false, [], Cancelled: true);

        var ex = Assert.Throws<JobFailedException>(() => result.EnsureSuccess());

        Assert.That(ex!.Message, Does.Contain("cancelled"));
    }
}
