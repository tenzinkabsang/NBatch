using Moq;
using NBatch.Readers.FileReader;
using NBatch.Readers.FileReader.Services;
using NUnit.Framework;

namespace NBatch.Tests.Readers.FileReaders;

[TestFixture]
public class CsvReaderTests
{
    private const string HeaderLine = "Name,Age,Score";

    private static CsvReader<(string Name, int Age)> CreateReader(IFileService fileService)
    {
        return new CsvReader<(string, int)>("fake.csv", row => (row.GetString("Name"), row.GetInt("Age")),
            fileService);
    }

    [TestCase(1, 1)]
    [TestCase(10, 10)]
    public async Task ReadAsync_maps_correct_number_of_items(int chunkSize, int expected)
    {
        var fileService = new Mock<IFileService>();

        // Header read
        fileService.Setup(f => f.ReadLinesAsync(0, 1, It.IsAny<CancellationToken>()))
            .Returns(new[] { HeaderLine }.ToAsyncEnumerable());

        // Data read — offset by 1 for the header row
        fileService.Setup(f => f.ReadLinesAsync(1, chunkSize, It.IsAny<CancellationToken>()))
            .Returns(Enumerable.Range(0, chunkSize).Select(_ => "Alice,30,100").ToAsyncEnumerable());

        var reader = CreateReader(fileService.Object);
        var results = (await reader.ReadAsync(0, chunkSize)).ToList();

        Assert.That(results, Has.Count.EqualTo(expected));
        Assert.That(results[0].Name, Is.EqualTo("Alice"));
        Assert.That(results[0].Age, Is.EqualTo(30));
    }

    [Test]
    public async Task ReadAsync_skips_blank_lines()
    {
        var fileService = new Mock<IFileService>();

        fileService.Setup(f => f.ReadLinesAsync(0, 1, It.IsAny<CancellationToken>()))
            .Returns(new[] { HeaderLine }.ToAsyncEnumerable());

        fileService.Setup(f => f.ReadLinesAsync(1, 3, It.IsAny<CancellationToken>()))
            .Returns(new[] { "Alice,30,100", "  ", "Bob,25,90" }.ToAsyncEnumerable());

        var reader = CreateReader(fileService.Object);
        var results = (await reader.ReadAsync(0, 3)).ToList();

        Assert.That(results, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task ReadAsync_returns_empty_when_no_data_lines()
    {
        var fileService = new Mock<IFileService>();

        fileService.Setup(f => f.ReadLinesAsync(0, 1, It.IsAny<CancellationToken>()))
            .Returns(new[] { HeaderLine }.ToAsyncEnumerable());

        fileService.Setup(f => f.ReadLinesAsync(1, 1, It.IsAny<CancellationToken>()))
            .Returns(Enumerable.Empty<string>().ToAsyncEnumerable());

        var reader = CreateReader(fileService.Object);
        var results = (await reader.ReadAsync(0, 1)).ToList();

        Assert.That(results, Is.Empty);
    }

    [Test]
    public async Task ReadAsync_auto_detects_headers_from_first_row()
    {
        var fileService = new Mock<IFileService>();

        fileService.Setup(f => f.ReadLinesAsync(0, 1, It.IsAny<CancellationToken>()))
            .Returns(new[] { "Name,Age,Score" }.ToAsyncEnumerable());

        fileService.Setup(f => f.ReadLinesAsync(1, 1, It.IsAny<CancellationToken>()))
            .Returns(new[] { "Charlie,40,95" }.ToAsyncEnumerable());

        var reader = CreateReader(fileService.Object);
        var results = (await reader.ReadAsync(0, 1)).ToList();

        Assert.That(results[0].Name, Is.EqualTo("Charlie"));
        Assert.That(results[0].Age, Is.EqualTo(40));
    }

    [Test]
    public async Task ReadAsync_with_custom_delimiter()
    {
        var fileService = new Mock<IFileService>();

        fileService.Setup(f => f.ReadLinesAsync(0, 1, It.IsAny<CancellationToken>()))
            .Returns(new[] { "Name|Age|Score" }.ToAsyncEnumerable());

        fileService.Setup(f => f.ReadLinesAsync(1, 1, It.IsAny<CancellationToken>()))
            .Returns(new[] { "Dana|28|88" }.ToAsyncEnumerable());

        var reader = new CsvReader<(string Name, int Age)>("fake.csv",
            row => (row.GetString("Name"), row.GetInt("Age")),
            fileService.Object)
            .WithDelimiter('|');

        var results = (await reader.ReadAsync(0, 1)).ToList();

        Assert.That(results[0].Name, Is.EqualTo("Dana"));
        Assert.That(results[0].Age, Is.EqualTo(28));
    }
}