using System.Globalization;
using Moq;
using NBatch.Core;
using NBatch.Core.Interfaces;
using NBatch.Readers.FileReader;
using NBatch.Readers.FileReader.Services;
using NUnit.Framework;

namespace NBatch.Tests.Readers.FileReaders;

[TestFixture]
internal sealed class CsvReaderAutoMapTests
{
    private enum Color { Red, Green, Blue }

    private sealed class Product
    {
        public string Name { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal Price { get; set; }
        public double Weight { get; set; }
        public bool Active { get; set; }
        public DateTime AddedOn { get; set; }
        public Guid Id { get; set; }
        public Color Color { get; set; }
        public int? Rating { get; set; }
        // Unsupported type — must be left unbound, not rejected.
        public List<string> Tags { get; set; } = [];
    }

    private sealed record PositionalRecord(string Name, int Age);

    private static Mock<IFileService> FileServiceWith(string header, params string[] dataLines)
    {
        var fileService = new Mock<IFileService>();

        // Catch-all first (later, more specific setups win): any read → empty.
        fileService.Setup(f => f.ReadLinesAsync(It.IsAny<long>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(AsyncEnumerable.Empty<string>());

        // Header line.
        fileService.Setup(f => f.ReadLinesAsync(0, 1, It.IsAny<CancellationToken>()))
            .Returns(new[] { header }.ToAsyncEnumerable());

        // Chunk reads starting at the first data line.
        fileService.Setup(f => f.ReadLinesAsync(1, It.Is<int>(n => n > 1), It.IsAny<CancellationToken>()))
            .Returns(dataLines.ToAsyncEnumerable());

        // Single-line reads at each data position (used by scan-mode re-reads).
        for (int i = 0; i < dataLines.Length; i++)
        {
            var line = dataLines[i];
            fileService.Setup(f => f.ReadLinesAsync(1 + i, 1, It.IsAny<CancellationToken>()))
                .Returns(new[] { line }.ToAsyncEnumerable());
        }

        return fileService;
    }

    [Test]
    public async Task Maps_headers_to_properties_case_insensitively_with_all_supported_types()
    {
        var id = Guid.NewGuid();
        var fileService = FileServiceWith(
            "name,QUANTITY,price,weight,active,addedOn,id,color,rating",
            $"Widget,7,19.99,1.25,true,2030-06-15T10:30:00,{id},green,4");

        var reader = new CsvReader<Product>("fake.csv", fileService.Object);
        var results = (await reader.ReadAsync(0, 5)).ToList();

        var product = results.Single();
        Assert.That(product.Name, Is.EqualTo("Widget"));
        Assert.That(product.Quantity, Is.EqualTo(7));
        Assert.That(product.Price, Is.EqualTo(19.99m));
        Assert.That(product.Weight, Is.EqualTo(1.25));
        Assert.That(product.Active, Is.True);
        Assert.That(product.AddedOn, Is.EqualTo(new DateTime(2030, 6, 15, 10, 30, 0)));
        Assert.That(product.Id, Is.EqualTo(id));
        Assert.That(product.Color, Is.EqualTo(Color.Green), "enum parse must ignore case");
        Assert.That(product.Rating, Is.EqualTo(4));
    }

    [Test]
    public async Task Ignores_unmatched_headers_and_leaves_unmatched_properties_default()
    {
        var fileService = FileServiceWith(
            "Name,UnknownColumn",
            "Widget,whatever");

        var reader = new CsvReader<Product>("fake.csv", fileService.Object);
        var product = (await reader.ReadAsync(0, 5)).Single();

        Assert.That(product.Name, Is.EqualTo("Widget"));
        Assert.That(product.Quantity, Is.EqualTo(0), "unmatched property stays default");
        Assert.That(product.Tags, Is.Empty, "unsupported property type stays unbound");
    }

    [Test]
    public async Task Nullable_property_with_empty_value_is_null()
    {
        var fileService = FileServiceWith(
            "Name,Rating",
            "Widget,");

        var reader = new CsvReader<Product>("fake.csv", fileService.Object);
        var product = (await reader.ReadAsync(0, 5)).Single();

        Assert.That(product.Rating, Is.Null);
    }

    [Test]
    public void NonNullable_property_with_empty_value_throws_with_line_number()
    {
        var fileService = FileServiceWith(
            "Name,Quantity",
            "Widget,");

        var reader = new CsvReader<Product>("fake.csv", fileService.Object);
        var ex = Assert.ThrowsAsync<FlatFileParseException>(() => reader.ReadAsync(0, 5));

        Assert.That(ex!.LineNumber, Is.EqualTo(2));
        Assert.That(ex.InnerException, Is.TypeOf<FormatException>());
    }

    [Test]
    public async Task Parsing_uses_invariant_culture()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            // de-DE uses ',' as the decimal separator — invariant parsing must still read "19.99".
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");

            var fileService = FileServiceWith(
                "Name,Price",
                "Widget,19.99");

            var reader = new CsvReader<Product>("fake.csv", fileService.Object);
            var product = (await reader.ReadAsync(0, 5)).Single();

            Assert.That(product.Price, Is.EqualTo(19.99m));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Test]
    public void Positional_record_without_parameterless_ctor_throws_at_construction()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            _ = new CsvReader<PositionalRecord>("fake.csv"));

        Assert.That(ex!.Message, Does.Contain("parameterless"));
        Assert.That(ex.Message, Does.Contain("Func<CsvRow, T>"));
    }

    [Test]
    public async Task AutoMap_failures_compose_with_skip_policy_for_FormatException()
    {
        // One bad row (unparseable quantity) among good rows: with scan mode and
        // inner-chain matching, only the bad row is skipped.
        var fileService = FileServiceWith(
            "Name,Quantity",
            "Good1,1", "Bad,not-a-number", "Good2,3");

        var writer = new List<Product>();
        var job = Job.CreateBuilder("automap-skip")
            .AddStep("import", step => step
                .ReadFrom(new CsvReader<Product>("fake.csv", fileService.Object))
                .WriteTo(items => { writer.AddRange(items); return Task.CompletedTask; })
                .WithSkipPolicy(SkipPolicy.For<FormatException>(maxSkips: 5))
                .WithChunkSize(5))
            .Build();

        var result = await job.RunAsync();

        Assert.That(result.Success, Is.True);
        Assert.That(result.Steps[0].ItemsSkipped, Is.EqualTo(1));
        Assert.That(writer.Select(p => p.Name), Is.EquivalentTo(new[] { "Good1", "Good2" }));
    }
}
