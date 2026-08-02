using Moq;
using NBatch.Readers.FileReader;
using NBatch.Readers.FileReader.Services;
using NUnit.Framework;

namespace NBatch.Tests.Readers.FileReaders;

[TestFixture]
internal class CsvReaderTests
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

    #region Quoted fields (RFC 4180)

    [Test]
    public async Task Quoted_field_with_embedded_delimiter()
    {
        var fileService = new Mock<IFileService>();

        fileService.Setup(f => f.ReadLinesAsync(0, 1, It.IsAny<CancellationToken>()))
            .Returns(new[] { HeaderLine }.ToAsyncEnumerable());
        fileService.Setup(f => f.ReadLinesAsync(1, 1, It.IsAny<CancellationToken>()))
            .Returns(new[] { "\"Smith, John\",30,100" }.ToAsyncEnumerable());

        var reader = CreateReader(fileService.Object);
        var results = (await reader.ReadAsync(0, 1)).ToList();

        Assert.That(results[0].Name, Is.EqualTo("Smith, John"));
        Assert.That(results[0].Age, Is.EqualTo(30));
    }

    [Test]
    public async Task Escaped_quotes_inside_quoted_field()
    {
        var fileService = new Mock<IFileService>();

        fileService.Setup(f => f.ReadLinesAsync(0, 1, It.IsAny<CancellationToken>()))
            .Returns(new[] { HeaderLine }.ToAsyncEnumerable());
        fileService.Setup(f => f.ReadLinesAsync(1, 1, It.IsAny<CancellationToken>()))
            .Returns(new[] { "\"Anna \"\"Ace\"\" Lee\",22,80" }.ToAsyncEnumerable());

        var reader = CreateReader(fileService.Object);
        var results = (await reader.ReadAsync(0, 1)).ToList();

        Assert.That(results[0].Name, Is.EqualTo("Anna \"Ace\" Lee"));
    }

    [Test]
    public async Task Quoted_header_fields_supported()
    {
        var fileService = new Mock<IFileService>();

        fileService.Setup(f => f.ReadLinesAsync(0, 1, It.IsAny<CancellationToken>()))
            .Returns(new[] { "\"Name\",\"Age\",\"Score\"" }.ToAsyncEnumerable());
        fileService.Setup(f => f.ReadLinesAsync(1, 1, It.IsAny<CancellationToken>()))
            .Returns(new[] { "Eve,33,70" }.ToAsyncEnumerable());

        var reader = CreateReader(fileService.Object);
        var results = (await reader.ReadAsync(0, 1)).ToList();

        Assert.That(results[0].Name, Is.EqualTo("Eve"));
        Assert.That(results[0].Age, Is.EqualTo(33));
    }

    [Test]
    public void Unterminated_quote_throws_FlatFileParseException_with_line_number()
    {
        var fileService = new Mock<IFileService>();

        fileService.Setup(f => f.ReadLinesAsync(0, 1, It.IsAny<CancellationToken>()))
            .Returns(new[] { HeaderLine }.ToAsyncEnumerable());
        fileService.Setup(f => f.ReadLinesAsync(1, 2, It.IsAny<CancellationToken>()))
            .Returns(new[] { "Alice,30,100", "\"broken,25,90" }.ToAsyncEnumerable());

        var reader = CreateReader(fileService.Object);
        var ex = Assert.ThrowsAsync<FlatFileParseException>(() => reader.ReadAsync(0, 2));

        Assert.That(ex!.LineNumber, Is.EqualTo(3)); // header = line 1, Alice = line 2
        Assert.That(ex.InnerException, Is.TypeOf<FormatException>());
    }

    #endregion

    #region Error semantics

    [Test]
    public void Parse_error_carries_line_number_and_inner_exception()
    {
        var fileService = new Mock<IFileService>();

        fileService.Setup(f => f.ReadLinesAsync(0, 1, It.IsAny<CancellationToken>()))
            .Returns(new[] { HeaderLine }.ToAsyncEnumerable());
        fileService.Setup(f => f.ReadLinesAsync(1, 3, It.IsAny<CancellationToken>()))
            .Returns(new[] { "Alice,30,100", "Bob,not-a-number,90", "Carol,25,80" }.ToAsyncEnumerable());

        var reader = CreateReader(fileService.Object);
        var ex = Assert.ThrowsAsync<FlatFileParseException>(() => reader.ReadAsync(0, 3));

        Assert.That(ex!.LineNumber, Is.EqualTo(3)); // header = 1, Alice = 2, Bob = 3
        Assert.That(ex.InnerException, Is.TypeOf<FormatException>());
    }

    [Test]
    public void Duplicate_headers_throw_FlatFileParseException()
    {
        var fileService = new Mock<IFileService>();

        fileService.Setup(f => f.ReadLinesAsync(0, 1, It.IsAny<CancellationToken>()))
            .Returns(new[] { "Name,Age,Name" }.ToAsyncEnumerable());

        var reader = CreateReader(fileService.Object);
        var ex = Assert.ThrowsAsync<FlatFileParseException>(() => reader.ReadAsync(0, 1));

        Assert.That(ex!.InnerException!.Message, Does.Contain("Name"));
    }

    [Test]
    public void Duplicate_explicit_headers_throw_ArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            new CsvReader<string>("fake.csv", row => row.GetString(0))
                .WithHeaders("Name", "Age", "Name"));
    }

    [Test]
    public void Missing_file_throws_FileNotFoundException_not_parse_exception()
    {
        // Real FileService against a nonexistent path: the I/O error must surface
        // raw — a missing file is not a skippable "parse error".
        var reader = new CsvReader<string>(
            Path.Combine(Path.GetTempPath(), $"nbatch_missing_{Guid.NewGuid():N}.csv"),
            row => row.GetString(0));

        Assert.ThrowsAsync<FileNotFoundException>(() => reader.ReadAsync(0, 1));
    }

    #endregion
}