using System.Linq;
using Moq;
using NBatch.Main.Readers.FileReader;
using NBatch.Main.Readers.FileReader.Services;
using NUnit.Framework;

namespace NBatch.Main.UnitTests.Readers.FileReaders
{
    [TestFixture]
    public class FlatFileItemReaderTests
    {
        private Mock<ILineMapper<string>> _lineMapper;

        [SetUp]
        public void BeforeEach()
        {
            _lineMapper = new Mock<ILineMapper<string>>();
            _lineMapper.SetupGet(x => x.Tokenizer).Returns(new Mock<ILineTokenizer>().Object);
        }

        [TestCase(1, 1)]
        [TestCase(10, 10)]
        public void MapIsCalledAsManyTimesAsChunkSize(int chunkSize, int expected)
        {
            var fileService = new Mock<IFileService>();
            fileService.Setup(f => f.ReadLines(0, chunkSize)).Returns(Enumerable.Range(0, chunkSize).Select(s => "item read"));

            var itemReader = new FlatFileItemReader<string>(_lineMapper.Object, fileService.Object);

            itemReader.Read(0, chunkSize);

            _lineMapper.Verify(m => m.MapToModel(It.IsAny<string>()), Times.Exactly(expected));
        }

        [TestCase(1, 9)]
        [TestCase(2, 8)]
        [TestCase(0, 10)]
        [TestCase(10, 0)]
        public void SkipsLinesOnlyWhenReadingForFirstTime(int linesToSkip, int expected)
        {
            const int CHUNK_SIZE = 10;
            var fileService = new Mock<IFileService>();
            fileService.Setup(f => f.ReadLines(0, CHUNK_SIZE)).Returns(Enumerable.Range(0, CHUNK_SIZE).Select(s => "item read"));
            var itemReader = new FlatFileItemReader<string>(_lineMapper.Object, fileService.Object)
                             {
                                 LinesToSkip = linesToSkip
                             };

            itemReader.Read(0, CHUNK_SIZE);

            _lineMapper.Verify(m => m.MapToModel(It.IsAny<string>()), Times.Exactly(expected));
        }

        [Test]
        public void LineMapperIsNotCalledIfItemIsNullOrEmpty()
        {
            var fileService = new Mock<IFileService>();
            fileService.Setup(f => f.ReadLines(0, 1)).Returns(Enumerable.Empty<string>());

            var itemReader = new FlatFileItemReader<string>(_lineMapper.Object, fileService.Object);

            itemReader.Read(0, 1);

            _lineMapper.Verify(m => m.MapToModel(It.IsAny<string>()), Times.Never());
        }
    }
}
