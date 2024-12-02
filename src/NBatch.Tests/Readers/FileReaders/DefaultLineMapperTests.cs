using Moq;
using NBatch.Readers.FileReader;
using NUnit.Framework;

namespace NBatch.Tests.Readers.FileReaders;

[TestFixture]
public class DefaultLineMapperTests
{
    DefaultLineMapper<string> _lineMapper;
    Mock<ILineTokenizer> _tokenizer;
    Mock<IFieldSetMapper<string>> _mapper;

    [SetUp]
    public void BeforeEach()
    {
        _tokenizer = new Mock<ILineTokenizer>();
        List<string> headers = ["NAME"];
        List<string> lines = ["Bob"];

        _tokenizer.SetupGet(x => x.Headers).Returns(headers.ToArray);
        _tokenizer.Setup(x => x.Tokenize(It.IsAny<string>())).Returns(FieldSet.Create(headers, lines));

        _mapper = new Mock<IFieldSetMapper<string>>();
        _lineMapper = new DefaultLineMapper<string>(_tokenizer.Object, _mapper.Object);
    }

    [Test]
    public void TokenizesTheDataBeforeMapping()
    {
        _lineMapper.MapToModel("line data");
        _tokenizer.Verify(x => x.Tokenize("line data"));
    }

    [Test]
    public void MapperReceivesAProperlyFilledFieldSet()
    {
        _lineMapper.MapToModel("line data");
        _mapper.Verify(m => m.MapFieldSet(It.Is<FieldSet>(fs => fs.GetString("NAME") == "Bob")));
    }
}
