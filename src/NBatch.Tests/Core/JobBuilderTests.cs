using NBatch.Core;
using NBatch.Core.Interfaces;
using NUnit.Framework;

namespace NBatch.Tests.Core;

/// <summary>
/// Tests for <see cref="JobBuilder"/> validation and job-level defaults.
/// </summary>
[TestFixture]
internal sealed class JobBuilderTests
{
    #region Helpers

    /// <summary>A reader that records the chunk size it is asked for.</summary>
    private sealed class ChunkSizeSpyReader : IReader<string>
    {
        public List<int> RequestedChunkSizes { get; } = [];

        public Task<IEnumerable<string>> ReadAsync(long startIndex, int chunkSize, CancellationToken cancellationToken = default)
        {
            RequestedChunkSizes.Add(chunkSize);
            return Task.FromResult(Enumerable.Empty<string>());
        }
    }

    private sealed class CollectingWriter<T> : IWriter<T>
    {
        public List<T> Written { get; } = [];

        public Task WriteAsync(IEnumerable<T> items, CancellationToken cancellationToken = default)
        {
            Written.AddRange(items);
            return Task.CompletedTask;
        }
    }

    private sealed class ListReader<T>(IReadOnlyList<T> items) : IReader<T>
    {
        public Task<IEnumerable<T>> ReadAsync(long startIndex, int chunkSize, CancellationToken cancellationToken = default)
            => Task.FromResult(items.Skip((int)startIndex).Take(chunkSize));
    }

    #endregion

    [Test]
    public void Build_throws_InvalidOperationException_when_no_steps_registered()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            Job.CreateBuilder("empty-job").Build());

        Assert.That(ex!.Message, Does.Contain("empty-job"));
        Assert.That(ex.Message, Does.Contain("AddStep"));
    }

    [Test]
    public async Task Default_chunk_size_applies_when_step_does_not_set_one()
    {
        var spy = new ChunkSizeSpyReader();

        var job = Job.CreateBuilder("default-chunk")
            .WithDefaultChunkSize(250)
            .AddStep("s1", step => step
                .ReadFrom(spy)
                .WriteTo(new CollectingWriter<string>()))
            .Build();

        await job.RunAsync();

        Assert.That(spy.RequestedChunkSizes, Has.All.EqualTo(250));
    }

    [Test]
    public async Task Per_step_WithChunkSize_overrides_default()
    {
        var spy = new ChunkSizeSpyReader();

        var job = Job.CreateBuilder("override-chunk")
            .WithDefaultChunkSize(250)
            .AddStep("s1", step => step
                .ReadFrom(spy)
                .WriteTo(new CollectingWriter<string>())
                .WithChunkSize(7))
            .Build();

        await job.RunAsync();

        Assert.That(spy.RequestedChunkSizes, Has.All.EqualTo(7));
    }

    [Test]
    public async Task Defaults_declared_after_AddStep_still_apply()
    {
        var spy = new ChunkSizeSpyReader();

        // WithDefaultChunkSize comes AFTER the step registration — must still apply.
        var job = Job.CreateBuilder("late-default")
            .AddStep("s1", step => step
                .ReadFrom(spy)
                .WriteTo(new CollectingWriter<string>()))
            .WithDefaultChunkSize(99)
            .Build();

        await job.RunAsync();

        Assert.That(spy.RequestedChunkSizes, Has.All.EqualTo(99));
    }

    [Test]
    public async Task Fallback_chunk_size_is_10_when_nothing_is_configured()
    {
        var spy = new ChunkSizeSpyReader();

        var job = Job.CreateBuilder("fallback-chunk")
            .AddStep("s1", step => step
                .ReadFrom(spy)
                .WriteTo(new CollectingWriter<string>()))
            .Build();

        await job.RunAsync();

        Assert.That(spy.RequestedChunkSizes, Has.All.EqualTo(10));
    }

    [Test]
    public async Task Default_skip_policy_is_inherited_by_steps()
    {
        var writer = new CollectingWriter<string>();

        var job = Job.CreateBuilder("default-skip")
            .WithDefaultSkipPolicy(SkipPolicy.For<FormatException>(maxSkips: 5))
            .AddStep("s1", step => step
                .ReadFrom(new ListReader<string>(["a", "bad", "c"]))
                .ProcessWith((string s) =>
                {
                    if (s == "bad") throw new FormatException("bad item");
                    return s;
                })
                .WriteTo(writer)
                .WithChunkSize(1))
            .Build();

        var result = await job.RunAsync();

        Assert.That(result.Success, Is.True);
        Assert.That(result.Steps[0].ItemsSkipped, Is.EqualTo(1));
        Assert.That(writer.Written, Is.EqualTo(new[] { "a", "c" }));
    }

    [Test]
    public async Task Per_step_skip_policy_overrides_default()
    {
        // Job default would tolerate FormatException, but the step's own policy
        // only tolerates TimeoutException — the FormatException must be fatal.
        var job = Job.CreateBuilder("override-skip")
            .WithDefaultSkipPolicy(SkipPolicy.For<FormatException>(maxSkips: 5))
            .AddStep("s1", step => step
                .ReadFrom(new ListReader<string>(["bad"]))
                .ProcessWith<string>((string s) => throw new FormatException("bad item"))
                .WriteTo(new CollectingWriter<string>())
                .WithSkipPolicy(SkipPolicy.For<TimeoutException>(maxSkips: 5))
                .WithChunkSize(1))
            .Build();

        var result = await job.RunAsync();

        Assert.That(result.Success, Is.False);
    }

    [Test]
    public void WithDefaultChunkSize_rejects_values_below_one()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Job.CreateBuilder("bad-default").WithDefaultChunkSize(0));
    }
}
