using NBatch.Core;
using NBatch.Readers.FileReader;
using NUnit.Framework;

namespace NBatch.Tests.Core;

[TestFixture]
internal sealed class RetryPolicyTests
{
    [Test]
    public void Constructor_rejects_non_exception_types()
    {
        Assert.Throws<ArgumentException>(() => _ = new RetryPolicy([typeof(string)], maxAttempts: 2, TimeSpan.Zero));
    }

    [Test]
    public void Constructor_rejects_max_attempts_below_one()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _ = new RetryPolicy([typeof(TimeoutException)], maxAttempts: 0, TimeSpan.Zero));
    }

    [Test]
    public void Constructor_rejects_negative_delay()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _ = new RetryPolicy([typeof(TimeoutException)], maxAttempts: 2, TimeSpan.FromSeconds(-1)));
    }

    [Test]
    public void None_never_matches()
    {
        Assert.That(RetryPolicy.None.Matches(new Exception()), Is.False);
    }

    [Test]
    public void Single_attempt_policy_never_matches()
    {
        var policy = RetryPolicy.For<TimeoutException>(maxAttempts: 1);
        Assert.That(policy.Matches(new TimeoutException()), Is.False);
    }

    [Test]
    public void Matches_honors_inheritance()
    {
        var policy = RetryPolicy.For<IOException>(maxAttempts: 3);
        Assert.That(policy.Matches(new FileNotFoundException()), Is.True);
    }

    [Test]
    public void Matches_honors_inner_exception_chain()
    {
        var policy = RetryPolicy.For<TimeoutException>(maxAttempts: 3);
        var wrapped = new FlatFileParseException(new TimeoutException());
        Assert.That(policy.Matches(wrapped), Is.True);
    }

    [Test]
    public void Matches_inner_chain_depth_limited_to_10()
    {
        var policy = RetryPolicy.For<TimeoutException>(maxAttempts: 3);

        Exception nested = new TimeoutException("root");
        for (int i = 0; i < 15; i++)
            nested = new InvalidOperationException($"wrapper {i}", nested);

        Assert.That(policy.Matches(nested), Is.False);
    }

    [Test]
    public void Does_not_match_unrelated_type()
    {
        var policy = RetryPolicy.For<TimeoutException>(maxAttempts: 3);
        Assert.That(policy.Matches(new FormatException()), Is.False);
    }

    [Test]
    public void Multi_type_factories_match_all_given_types()
    {
        var policy = RetryPolicy.For<TimeoutException, IOException>(maxAttempts: 2);
        Assert.That(policy.Matches(new TimeoutException()), Is.True);
        Assert.That(policy.Matches(new IOException()), Is.True);
        Assert.That(policy.Matches(new FormatException()), Is.False);
    }

    [Test]
    public void GetDelay_is_constant_without_backoff()
    {
        var policy = RetryPolicy.For<TimeoutException>(maxAttempts: 4, TimeSpan.FromSeconds(2));

        Assert.That(policy.GetDelay(1), Is.EqualTo(TimeSpan.FromSeconds(2)));
        Assert.That(policy.GetDelay(3), Is.EqualTo(TimeSpan.FromSeconds(2)));
    }

    [Test]
    public void GetDelay_applies_backoff_multiplier()
    {
        var policy = RetryPolicy.For<TimeoutException>(maxAttempts: 4, TimeSpan.FromSeconds(1))
            .WithBackoffMultiplier(2.0);

        Assert.That(policy.GetDelay(1), Is.EqualTo(TimeSpan.FromSeconds(1)));
        Assert.That(policy.GetDelay(2), Is.EqualTo(TimeSpan.FromSeconds(2)));
        Assert.That(policy.GetDelay(3), Is.EqualTo(TimeSpan.FromSeconds(4)));
    }

    [Test]
    public void WithBackoffMultiplier_returns_new_instance_and_validates()
    {
        var policy = RetryPolicy.For<TimeoutException>(maxAttempts: 3, TimeSpan.FromSeconds(1));
        var withBackoff = policy.WithBackoffMultiplier(1.5);

        Assert.That(withBackoff, Is.Not.SameAs(policy));
        Assert.That(policy.GetDelay(2), Is.EqualTo(TimeSpan.FromSeconds(1)), "original policy must be unchanged");
        Assert.Throws<ArgumentOutOfRangeException>(() => policy.WithBackoffMultiplier(0.5));
    }
}
