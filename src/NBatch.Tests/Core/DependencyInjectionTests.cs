using Microsoft.Extensions.DependencyInjection;
using NBatch.Core;
using NBatch.Core.Interfaces;
using NUnit.Framework;

namespace NBatch.Tests.Core;

[TestFixture]
internal sealed class DependencyInjectionTests
{
    #region Helpers

    private sealed class ListReader<T>(IReadOnlyList<T> items) : IReader<T>
    {
        public Task<IEnumerable<T>> ReadAsync(long startIndex, int chunkSize, CancellationToken cancellationToken = default)
        {
            var chunk = items.Skip((int)startIndex).Take(chunkSize);
            return Task.FromResult(chunk);
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

    #endregion

    [Test]
    public void AddNBatch_registers_IJobRunner()
    {
        var services = new ServiceCollection();

        services.AddNBatch(nbatch =>
        {
            nbatch.AddJob("test-job", job => job
                .AddStep("step1", step => step
                    .ReadFrom(new ListReader<string>(["a", "b"]))
                    .WriteTo(new CollectingWriter<string>())));
        });

        var sp = services.BuildServiceProvider();
        var runner = sp.GetService<IJobRunner>();

        Assert.That(runner, Is.Not.Null);
    }

    [Test]
    public async Task RunAsync_executes_registered_job()
    {
        var writer = new CollectingWriter<string>();
        var services = new ServiceCollection();

        services.AddNBatch(nbatch =>
        {
            nbatch.AddJob("test-job", job => job
                .AddStep("step1", step => step
                    .ReadFrom(new ListReader<string>(["a", "b", "c"]))
                    .WriteTo(writer)
                    .WithChunkSize(2)));
        });

        var sp = services.BuildServiceProvider();
        var runner = sp.GetRequiredService<IJobRunner>();

        var result = await runner.RunAsync("test-job");

        Assert.That(result.Success, Is.True);
        Assert.That(result.Name, Is.EqualTo("test-job"));
        Assert.That(writer.Written, Is.EqualTo(new[] { "a", "b", "c" }));
    }

    [Test]
    public void RunAsync_throws_for_unregistered_job()
    {
        var services = new ServiceCollection();

        services.AddNBatch(nbatch =>
        {
            nbatch.AddJob("real-job", job => job
                .AddStep("step1", step => step
                    .ReadFrom(new ListReader<string>([]))
                    .WriteTo(new CollectingWriter<string>())));
        });

        var sp = services.BuildServiceProvider();
        var runner = sp.GetRequiredService<IJobRunner>();

        Assert.ThrowsAsync<ArgumentException>(() => runner.RunAsync("no-such-job"));
    }

    [Test]
    public async Task AddJob_with_service_provider_resolves_dependencies()
    {
        var writer = new CollectingWriter<int>();
        var services = new ServiceCollection();
        services.AddSingleton(writer);

        services.AddNBatch(nbatch =>
        {
            nbatch.AddJob("di-job", (sp, job) => job
                .AddStep("step1", step => step
                    .ReadFrom(new ListReader<int>([1, 2, 3]))
                    .WriteTo(sp.GetRequiredService<CollectingWriter<int>>())
                    .WithChunkSize(10)));
        });

        var sp = services.BuildServiceProvider();
        var runner = sp.GetRequiredService<IJobRunner>();

        var result = await runner.RunAsync("di-job");

        Assert.That(result.Success, Is.True);
        Assert.That(writer.Written, Is.EqualTo(new[] { 1, 2, 3 }));
    }

    [Test]
    public async Task Multiple_jobs_can_be_registered_and_run_independently()
    {
        var writer1 = new CollectingWriter<string>();
        var writer2 = new CollectingWriter<int>();
        var services = new ServiceCollection();

        services.AddNBatch(nbatch =>
        {
            nbatch.AddJob("strings", job => job
                .AddStep("s1", step => step
                    .ReadFrom(new ListReader<string>(["x", "y"]))
                    .WriteTo(writer1)));

            nbatch.AddJob("numbers", job => job
                .AddStep("s1", step => step
                    .ReadFrom(new ListReader<int>([1, 2]))
                    .WriteTo(writer2)));
        });

        var sp = services.BuildServiceProvider();
        var runner = sp.GetRequiredService<IJobRunner>();

        await runner.RunAsync("strings");
        await runner.RunAsync("numbers");

        Assert.That(writer1.Written, Is.EqualTo(new[] { "x", "y" }));
        Assert.That(writer2.Written, Is.EqualTo(new[] { 1, 2 }));
    }

    [Test]
    public async Task RunAsync_passes_cancellation_token_to_job()
    {
        var services = new ServiceCollection();
        var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        services.AddNBatch(nbatch =>
        {
            nbatch.AddJob("cancel-job", job => job
                .AddStep("step1", step => step
                    .ReadFrom(new ListReader<string>(["a"]))
                    .WriteTo(new CollectingWriter<string>())));
        });

        var sp = services.BuildServiceProvider();
        var runner = sp.GetRequiredService<IJobRunner>();

        Assert.ThrowsAsync<OperationCanceledException>(() => runner.RunAsync("cancel-job", cts.Token));
    }
}
