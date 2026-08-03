using System.Globalization;
using Moq;
using NBatch.Writers.FileWriter;
using NUnit.Framework;

namespace NBatch.Tests.Writers;

[TestFixture]
internal class FlatFileItemWriterTests
{
    private sealed class Record
    {
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
    }

    [Test]
    public void Serializer_quotes_fields_containing_the_delimiter()
    {
        var serializer = new PropertyValueSerializer();

        var lines = serializer.Serialize([new Record { Name = "Smith, John", Price = 5m }]).ToList();

        Assert.That(lines.Single(), Is.EqualTo("\"Smith, John\",5"));
    }

    [Test]
    public void Serializer_escapes_embedded_quotes_by_doubling()
    {
        var serializer = new PropertyValueSerializer();

        var lines = serializer.Serialize([new Record { Name = "Anna \"Ace\" Lee", Price = 1m }]).ToList();

        Assert.That(lines.Single(), Is.EqualTo("\"Anna \"\"Ace\"\" Lee\",1"));
    }

    [Test]
    public void Serializer_does_not_quote_plain_values()
    {
        var serializer = new PropertyValueSerializer();

        var lines = serializer.Serialize([new Record { Name = "Plain", Price = 2.5m }]).ToList();

        Assert.That(lines.Single(), Is.EqualTo("Plain,2.5"));
    }

    [Test]
    public void Serializer_respects_custom_delimiter_when_quoting()
    {
        var serializer = new PropertyValueSerializer('|');

        var lines = serializer.Serialize([new Record { Name = "a|b", Price = 3m }]).ToList();

        Assert.That(lines.Single(), Is.EqualTo("\"a|b\"|3"));
    }

    [Test]
    public void Serializer_formats_with_invariant_culture()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            // de-DE formats 19.99 as "19,99" — which would corrupt a comma-delimited
            // file and parse differently per machine locale.
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            var serializer = new PropertyValueSerializer();

            var lines = serializer.Serialize([new Record { Name = "Widget", Price = 19.99m }]).ToList();

            Assert.That(lines.Single(), Is.EqualTo("Widget,19.99"));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Test]
    public async Task CallsPropertyValueSerializerToReadAllPropValues()
    {
        var propSerializer = new Mock<IPropertyValueSerializer>();
        var fileService = new Mock<IFileWriterService>();

        var fileWriter = new FlatFileItemWriter<string>(propSerializer.Object, fileService.Object);

        string[] items = ["one", "two"];

        await fileWriter.WriteAsync(items);

        propSerializer.Verify(p => p.Serialize(items));
    }

    [Test]
    public async Task CallsFileServiceToWriteItemsToFile()
    {
        string[] items = ["one"];
        var propSerializer = new Mock<IPropertyValueSerializer>();
        propSerializer.Setup(s => s.Serialize(It.IsAny<IEnumerable<object>>())).Returns(items);

        var fileService = new Mock<IFileWriterService>();

        var fileWriter = new FlatFileItemWriter<string>(propSerializer.Object, fileService.Object);

        await fileWriter.WriteAsync(items);

        fileService.Verify(f => f.WriteFileAsync(items, It.IsAny<CancellationToken>()));
    }
}
