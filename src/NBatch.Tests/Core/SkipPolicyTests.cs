using NBatch.Core;
using NBatch.Readers.FileReader;
using NUnit.Framework;

namespace NBatch.Tests.Core;

[TestFixture]
internal sealed class SkipPolicyTests
{
    [Test]
    public void Matches_exact_type()
    {
        var policy = SkipPolicy.For<FormatException>(maxSkips: 1);
        Assert.That(policy.Matches(new FormatException()), Is.True);
    }

    [Test]
    public void Matches_derived_type()
    {
        // A policy for the base type matches subclasses.
        var policy = SkipPolicy.For<IOException>(maxSkips: 1);
        Assert.That(policy.Matches(new FileNotFoundException()), Is.True);
    }

    [Test]
    public void Matches_wrapped_inner_exception()
    {
        // A policy for the inner type matches the wrapping exception.
        var policy = SkipPolicy.For<FormatException>(maxSkips: 1);
        var wrapped = new FlatFileParseException(new FormatException("bad number"));
        Assert.That(policy.Matches(wrapped), Is.True);
    }

    [Test]
    public void Matches_wrapper_type_directly()
    {
        var policy = SkipPolicy.For<FlatFileParseException>(maxSkips: 1);
        var wrapped = new FlatFileParseException(new FormatException("bad number"));
        Assert.That(policy.Matches(wrapped), Is.True);
    }

    [Test]
    public void Matches_walks_inner_chain_to_bounded_depth()
    {
        var policy = SkipPolicy.For<FormatException>(maxSkips: 1);

        // 5 wrappers deep — within the depth bound of 10.
        Exception nested = new FormatException("root");
        for (int i = 0; i < 5; i++)
            nested = new InvalidOperationException($"wrapper {i}", nested);
        Assert.That(policy.Matches(nested), Is.True);

        // 15 wrappers deep — beyond the bound, no match.
        nested = new FormatException("root");
        for (int i = 0; i < 15; i++)
            nested = new InvalidOperationException($"wrapper {i}", nested);
        Assert.That(policy.Matches(nested), Is.False);
    }

    [Test]
    public void Does_not_match_unrelated_type()
    {
        var policy = SkipPolicy.For<TimeoutException>(maxSkips: 1);
        Assert.That(policy.Matches(new InvalidOperationException()), Is.False);
    }

    [Test]
    public void None_never_matches()
    {
        Assert.That(SkipPolicy.None.Matches(new Exception()), Is.False);
    }

    [Test]
    public void Zero_limit_never_matches()
    {
        var policy = new SkipPolicy([typeof(Exception)], skipLimit: 0);
        Assert.That(policy.Matches(new FormatException()), Is.False);
    }

    [Test]
    public void Multiple_types_all_match()
    {
        var policy = SkipPolicy.For<FormatException, TimeoutException>(maxSkips: 1);
        Assert.That(policy.Matches(new FormatException()), Is.True);
        Assert.That(policy.Matches(new TimeoutException()), Is.True);
        Assert.That(policy.Matches(new InvalidOperationException()), Is.False);
    }

    [Test]
    public void Non_exception_type_throws_at_construction()
    {
        Assert.Throws<ArgumentException>(() => _ = new SkipPolicy([typeof(string)], skipLimit: 1));
    }
}
