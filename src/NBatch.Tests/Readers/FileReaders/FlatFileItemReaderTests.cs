using Moq;
using NBatch.Readers.FileReader;
using NBatch.Readers.FileReader.Services;
using NUnit.Framework;

namespace NBatch.Tests.Readers.FileReaders;

[TestFixture]
public class  FlatFileItemReaderTests
{
    Mock<ILineMapper<string>> _lineMapper;

    [SetUp]
    public void BeforeEach()
    {
        _lineMapper = new Mock<ILineMapper<string>>();
        _lineMapper.SetupGet(x => x.Tokenizer).Returns(new Mock<ILineTokenizer>().Object);
    }

    [TestCase(1, 1)]
    [TestCase(10, 10)]
    public async Task MapIsCalledAsManyTimesAsChunkSize(int chunkSize, int expected)
    {
        var fileService = new Mock<IFileService>();
        fileService.Setup(f => f.ReadLinesAsync(0, chunkSize))
            .Returns(Enumerable.Range(0, chunkSize).ToAsyncEnumerable().Select(s => "item read"));

        var itemReader = new FlatFileItemReader<string>(_lineMapper.Object, fileService.Object);

        await itemReader.ReadAsync(0, chunkSize);

        _lineMapper.Verify(m => m.MapToModel(It.IsAny<string>()), Times.Exactly(expected));
    }

    [TestCase(1, 9)]
    [TestCase(2, 8)]
    [TestCase(0, 10)]
    [TestCase(10, 0)]
    public async Task SkipsLinesForHeadersOnlyWhenReadingForFirstTime(int linesToSkip, int expected)
    {
        const int CHUNK_SIZE = 10; /** Use same chunk size for all tests **/
        var fileService = new Mock<IFileService>();
        fileService.Setup(f => f.ReadLinesAsync(0, CHUNK_SIZE))
            .Returns(Enumerable.Range(0, CHUNK_SIZE).ToAsyncEnumerable().Select(s => "item read"));


        var itemReader = new FlatFileItemReader<string>(_lineMapper.Object, fileService.Object);
        itemReader.LinesToSkip = linesToSkip;

        await itemReader.ReadAsync(0, CHUNK_SIZE);

        _lineMapper.Verify(m => m.MapToModel(It.IsAny<string>()), Times.Exactly(expected));
    }

    [Test]
    public async Task LineMapperIsNotCalledIfItemIsNullOrEmpty()
    {
        var fileService = new Mock<IFileService>();
        fileService.Setup(f => f.ReadLinesAsync(0, 1))
            .Returns(Enumerable.Empty<string>().ToAsyncEnumerable());

        var itemReader = new FlatFileItemReader<string>(_lineMapper.Object, fileService.Object);

        await itemReader.ReadAsync(0, 1);

        _lineMapper.Verify(m => m.MapToModel(It.IsAny<string>()), Times.Never());
    }
}