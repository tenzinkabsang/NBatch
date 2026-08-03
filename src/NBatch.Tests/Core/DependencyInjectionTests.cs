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
    public void AddJob_with_duplicate_name_throws_ArgumentException()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentException>(() =>
            services.AddNBatch(nbatch =>
            {
                nbatch.AddJob("dup-job", job => job
                    .AddStep("s1", step => step.Execute(() => { })));
                nbatch.AddJob("dup-job", job => job
                    .AddStep("s1", step => step.Execute(() => { })));
            }));
    }

    [Test]
    public void AddJob_with_duplicate_name_throws_for_service_provider_overload()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentException>(() =>
            services.AddNBatch(nbatch =>
            {
                nbatch.AddJob("dup-job", job => job
                    .AddStep("s1", step => step.Execute(() => { })));
                nbatch.AddJob("dup-job", (sp, job) => job
                    .AddStep("s1", step => step.Execute(() => { })));
            }));
    }

    #region Type-based (DI-resolved) components

    private sealed class FixedReader : IReader<string>
    {
        public Task<IEnumerable<string>> ReadAsync(long startIndex, int chunkSize, CancellationToken cancellationToken = default)
            => Task.FromResult(startIndex == 0 ? new[] { "r1", "r2" }.AsEnumerable() : []);
    }

    private sealed class UppercaseProcessor : IProcessor<string, string>
    {
        public Task<string> ProcessAsync(string input, CancellationToken cancellationToken = default)
            => Task.FromResult(input.ToUpperInvariant());
    }

    private sealed class SharedSink
    {
        public List<string> Items { get; } = [];
        public List<Guid> WriterInstances { get; } = [];
    }

    private sealed class SinkWriter(SharedSink sink) : IWriter<string>
    {
        private readonly Guid _instanceId = Guid.NewGuid();

        public Task WriteAsync(IEnumerable<string> items, CancellationToken cancellationToken = default)
        {
            sink.Items.AddRange(items);
            sink.WriterInstances.Add(_instanceId);
            return Task.CompletedTask;
        }
    }

    private sealed class CountingTasklet(SharedSink sink) : ITasklet
    {
        public Task ExecuteAsync(CancellationToken cancellationToken = default)
        {
            sink.Items.Add("tasklet-ran");
            return Task.CompletedTask;
        }
    }

    [Test]
    public async Task TypeBased_components_resolve_registered_services()
    {
        var services = new ServiceCollection();
        services.AddSingleton<SharedSink>();
        services.AddScoped<FixedReader>();
        services.AddScoped<UppercaseProcessor>();
        services.AddScoped<SinkWriter>();

        services.AddNBatch(nbatch =>
        {
            nbatch.AddJob("typed-job", job => job
                .AddStep("etl", step => step
                    .ReadFrom<FixedReader, string>()
                    .ProcessWith<UppercaseProcessor, string>()
                    .WriteTo<SinkWriter>()));
        });

        var sp = services.BuildServiceProvider();
        var sink = sp.GetRequiredService<SharedSink>();

        var result = await sp.GetRequiredService<IJobRunner>().RunAsync("typed-job");

        Assert.That(result.Success, Is.True);
        Assert.That(sink.Items, Is.EqualTo(new[] { "R1", "R2" }));
    }

    [Test]
    public async Task TypeBased_Execute_resolves_tasklet()
    {
        var services = new ServiceCollection();
        services.AddSingleton<SharedSink>();

        services.AddNBatch(nbatch =>
        {
            nbatch.AddJob("tasklet-job", job => job
                .AddStep("notify", step => step.Execute<CountingTasklet>()));
        });

        var sp = services.BuildServiceProvider();
        var result = await sp.GetRequiredService<IJobRunner>().RunAsync("tasklet-job");

        Assert.That(result.Success, Is.True);
        Assert.That(sp.GetRequiredService<SharedSink>().Items, Is.EqualTo(new[] { "tasklet-ran" }));
    }

    [Test]
    public async Task Unregistered_component_is_constructed_via_ActivatorUtilities()
    {
        // SinkWriter is NOT registered, but its SharedSink dependency is —
        // ActivatorUtilities must construct it with the dependency supplied by DI.
        var services = new ServiceCollection();
        services.AddSingleton<SharedSink>();

        services.AddNBatch(nbatch =>
        {
            nbatch.AddJob("activator-job", job => job
                .AddStep("s1", step => step
                    .ReadFrom(new ListReader<string>(["a"]))
                    .WriteTo<SinkWriter>()));
        });

        var sp = services.BuildServiceProvider();
        var result = await sp.GetRequiredService<IJobRunner>().RunAsync("activator-job");

        Assert.That(result.Success, Is.True);
        Assert.That(sp.GetRequiredService<SharedSink>().Items, Is.EqualTo(new[] { "a" }));
    }

    [Test]
    public async Task Scoped_component_is_fresh_per_run()
    {
        var services = new ServiceCollection();
        services.AddSingleton<SharedSink>();
        services.AddScoped<SinkWriter>();

        services.AddNBatch(nbatch =>
        {
            nbatch.AddJob("scoped-job", job => job
                .AddStep("s1", step => step
                    .ReadFrom(new ListReader<string>(["a"]))
                    .WriteTo<SinkWriter>()));
        });

        var sp = services.BuildServiceProvider();
        var runner = sp.GetRequiredService<IJobRunner>();
        var sink = sp.GetRequiredService<SharedSink>();

        await runner.RunAsync("scoped-job");
        await runner.RunAsync("scoped-job");

        Assert.That(sink.WriterInstances, Has.Count.EqualTo(2));
        Assert.That(sink.WriterInstances[0], Is.Not.EqualTo(sink.WriterInstances[1]),
            "each run gets a fresh scoped writer instance");
    }

    [Test]
    public void TypeBased_overload_on_standalone_builder_throws_InvalidOperationException()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            Job.CreateBuilder("standalone")
                .AddStep("s1", step => step
                    .ReadFrom<FixedReader, string>()
                    .WriteTo(new CollectingWriter<string>()))
                .Build());

        Assert.That(ex!.Message, Does.Contain("AddNBatch"));
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
