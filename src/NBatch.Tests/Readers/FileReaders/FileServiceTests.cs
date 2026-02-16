using NBatch.Readers.FileReader;
using NBatch.Readers.FileReader.Services;
using NUnit.Framework;

namespace NBatch.Tests.Readers.FileReaders;

[TestFixture]
internal class FileServiceTests
{
    private string _tempFile = null!;

    [SetUp]
    public void Setup()
    {
        _tempFile = Path.GetTempFileName();
        File.WriteAllLines(_tempFile,
        [
            "header",
            "line1",
            "line2",
            "line3",
            "line4",
            "line5",
            "line6",
            "line7",
            "line8",
            "line9",
            "line10"
        ]);
    }

    [TearDown]
    public void Teardown()
    {
        if (File.Exists(_tempFile))
            File.Delete(_tempFile);
    }

    [Test]
    public async Task ReadLinesAsync_reads_first_chunk()
    {
        using var service = new FileService(_tempFile);

        var lines = await service.ReadLinesAsync(0, 3).ToListAsync();

        Assert.That(lines, Is.EqualTo(new[] { "header", "line1", "line2" }));
    }

    [Test]
    public async Task ReadLinesAsync_sequential_chunks_read_without_reskipping()
    {
        using var service = new FileService(_tempFile);

        var chunk1 = await service.ReadLinesAsync(0, 3).ToListAsync();
        var chunk2 = await service.ReadLinesAsync(3, 3).ToListAsync();
        var chunk3 = await service.ReadLinesAsync(6, 3).ToListAsync();

        Assert.That(chunk1, Is.EqualTo(new[] { "header", "line1", "line2" }));
        Assert.That(chunk2, Is.EqualTo(new[] { "line3", "line4", "line5" }));
        Assert.That(chunk3, Is.EqualTo(new[] { "line6", "line7", "line8" }));
    }

    [Test]
    public async Task ReadLinesAsync_returns_remaining_lines_when_chunk_exceeds_file()
    {
        using var service = new FileService(_tempFile);

        var lines = await service.ReadLinesAsync(8, 100).ToListAsync();

        Assert.That(lines, Is.EqualTo(new[] { "line8", "line9", "line10" }));
    }

    [Test]
    public async Task ReadLinesAsync_returns_empty_when_startIndex_past_end()
    {
        using var service = new FileService(_tempFile);

        var lines = await service.ReadLinesAsync(100, 5).ToListAsync();

        Assert.That(lines, Is.Empty);
    }

    [Test]
    public async Task ReadLinesAsync_resets_when_startIndex_goes_backward()
    {
        using var service = new FileService(_tempFile);

        // Read chunk starting at line 5
        var first = await service.ReadLinesAsync(5, 2).ToListAsync();
        // Now request an earlier line — must reset the reader
        var second = await service.ReadLinesAsync(1, 2).ToListAsync();

        Assert.That(first, Is.EqualTo(new[] { "line5", "line6" }));
        Assert.That(second, Is.EqualTo(new[] { "line1", "line2" }));
    }

    [Test]
    public async Task ReadLinesAsync_handles_non_contiguous_forward_skip()
    {
        using var service = new FileService(_tempFile);

        // Read chunk 0–2, then skip ahead to 6
        var chunk1 = await service.ReadLinesAsync(0, 3).ToListAsync();
        var chunk2 = await service.ReadLinesAsync(6, 3).ToListAsync();

        Assert.That(chunk1, Is.EqualTo(new[] { "header", "line1", "line2" }));
        Assert.That(chunk2, Is.EqualTo(new[] { "line6", "line7", "line8" }));
    }

    [Test]
    public async Task ReadLinesAsync_single_line_file()
    {
        File.WriteAllText(_tempFile, "only-line");
        using var service = new FileService(_tempFile);

        var chunk1 = await service.ReadLinesAsync(0, 10).ToListAsync();
        var chunk2 = await service.ReadLinesAsync(1, 10).ToListAsync();

        Assert.That(chunk1, Is.EqualTo(new[] { "only-line" }));
        Assert.That(chunk2, Is.Empty);
    }

    [Test]
    public async Task ReadLinesAsync_empty_file()
    {
        File.WriteAllText(_tempFile, "");
        using var service = new FileService(_tempFile);

        var lines = await service.ReadLinesAsync(0, 10).ToListAsync();

        Assert.That(lines, Is.Empty);
    }

    [Test]
    public async Task ReadLinesAsync_header_then_sequential_data_mirrors_CsvReader_pattern()
    {
        // Simulates exactly what CsvReader does:
        //   1. Read line 0 (header)
        //   2. Read lines 1–3 (chunk 0, adjustedIndex = startIndex + 1)
        //   3. Read lines 4–6 (chunk 1)
        using var service = new FileService(_tempFile);

        // Header read — CsvReader reads (0, 1)
        var header = await service.ReadLinesAsync(0, 1).ToListAsync();
        // Chunk 0 — CsvReader reads (adjustedIndex=1, chunkSize=3)
        var chunk0 = await service.ReadLinesAsync(1, 3).ToListAsync();
        // Chunk 1 — CsvReader reads (adjustedIndex=4, chunkSize=3)
        var chunk1 = await service.ReadLinesAsync(4, 3).ToListAsync();

        Assert.That(header, Is.EqualTo(new[] { "header" }));
        Assert.That(chunk0, Is.EqualTo(new[] { "line1", "line2", "line3" }));
        Assert.That(chunk1, Is.EqualTo(new[] { "line4", "line5", "line6" }));
    }

    [Test]
    public async Task Separate_FileService_instances_are_independent()
    {
        // Two steps reading the same file get separate FileService instances
        using var service1 = new FileService(_tempFile);
        using var service2 = new FileService(_tempFile);

        // Service1 reads chunk 0
        var s1chunk = await service1.ReadLinesAsync(0, 3).ToListAsync();
        // Service2 reads chunk 0 independently — not affected by service1's position
        var s2chunk = await service2.ReadLinesAsync(0, 3).ToListAsync();

        Assert.That(s1chunk, Is.EqualTo(new[] { "header", "line1", "line2" }));
        Assert.That(s2chunk, Is.EqualTo(new[] { "header", "line1", "line2" }));

        // Both continue independently
        var s1next = await service1.ReadLinesAsync(3, 2).ToListAsync();
        var s2next = await service2.ReadLinesAsync(5, 2).ToListAsync();

        Assert.That(s1next, Is.EqualTo(new[] { "line3", "line4" }));
        Assert.That(s2next, Is.EqualTo(new[] { "line5", "line6" }));
    }

    [Test]
    public async Task CsvReader_end_to_end_reads_all_chunks_from_real_file()
    {
        // Write a proper CSV
        File.WriteAllLines(_tempFile,
        [
            "Name,Age",
            "Alice,30",
            "Bob,25",
            "Charlie,40",
            "Dana,28",
            "Eve,35"
        ]);

        using var reader = new CsvReader<(string Name, int Age)>(_tempFile,
            row => (row.GetString("Name"), row.GetInt("Age")));

        // Chunk 0: lines 1–2 (startIndex=0, chunkSize=2, adjustedIndex=1)
        var chunk0 = (await reader.ReadAsync(0, 2)).ToList();
        // Chunk 1: lines 3–4 (startIndex=2, chunkSize=2, adjustedIndex=3)
        var chunk1 = (await reader.ReadAsync(2, 2)).ToList();
        // Chunk 2: line 5 only (startIndex=4, chunkSize=2, adjustedIndex=5)
        var chunk2 = (await reader.ReadAsync(4, 2)).ToList();
        // Chunk 3: past end (startIndex=6, adjustedIndex=7)
        var chunk3 = (await reader.ReadAsync(6, 2)).ToList();

        Assert.That(chunk0, Is.EqualTo(new[] { ("Alice", 30), ("Bob", 25) }));
        Assert.That(chunk1, Is.EqualTo(new[] { ("Charlie", 40), ("Dana", 28) }));
        Assert.That(chunk2, Is.EqualTo(new[] { ("Eve", 35) }));
        Assert.That(chunk3, Is.Empty);
    }
}
