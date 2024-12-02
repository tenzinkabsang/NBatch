using Moq;
using NBatch.Writers.FileWriter;
using NUnit.Framework;

namespace NBatch.Tests.Writers;

[TestFixture]
public class FlatFileItemWriterTests
{
    [Test]
    public async Task CallsPropertyValueSerializerToReadAllPropValues()
    {
        var propSerializer = new Mock<IPropertyValueSerializer>();
        var fileService = new Mock<IFileWriterService>();

        var fileWriter = new FlatFileItemWriter<string>(propSerializer.Object, fileService.Object);

        string[] items = ["one", "two"];

        var result = await fileWriter.WriteAsync(items);

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

        bool isSaved = await fileWriter.WriteAsync(items);

        Assert.That(isSaved, Is.True);
        fileService.Verify(f => f.WriteFileAsync(items));
    }
}
