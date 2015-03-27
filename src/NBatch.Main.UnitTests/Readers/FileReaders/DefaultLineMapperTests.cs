using System.Collections.Generic;
using Moq;
using NBatch.Main.Readers.FileReaders;
using NUnit.Framework;

namespace NBatch.Main.UnitTests.Readers.FileReaders
{
    [TestFixture]
    class DefaultLineMapperTests
    {
        DefaultLineMapper<string> _lineMapper;
        Mock<ILineTokenizer> _lineTokenizer;
        Mock<IFieldSetMapper<string>> _mapper;

        [SetUp]
        public void BeforeEach()
        {
            _lineTokenizer = new Mock<ILineTokenizer>();
            var headers = new List<string> {"NAME"};
            var line = new List<string> {"Bob"};

            _lineTokenizer.SetupGet(x => x.Headers).Returns(headers.ToArray);
            _lineTokenizer.Setup(x => x.Tokenize(It.IsAny<string>())).Returns(FieldSet.Create(headers, line));

            _mapper = new Mock<IFieldSetMapper<string>>();
            _lineMapper = new DefaultLineMapper<string>(_lineTokenizer.Object, _mapper.Object);
        }

        [Test]
        public void TokenizesTheDataBeforeMapping()
        {
            _lineMapper.MapToModel("line");
            _lineTokenizer.Verify(x => x.Tokenize("line"));
        }

        [Test]
        public void MapperReceivesAProperlyFilledFieldSet()
        {
            _lineMapper.MapToModel("line");
            _mapper.Verify(m => m.MapFieldSet(It.Is<FieldSet>(fs => fs.GetString("NAME") == "Bob")));
        }
    }
}
