using System.Collections.Generic;
using System.Text;
using Moq;
using NBatch.Main.Readers.FileReader.Services;
using NBatch.Main.Writers.FileWriter;
using NUnit.Framework;

namespace NBatch.Main.UnitTests.Writers.FileWriter
{
    [TestFixture]
    class FlatFileItemWriterTests
    {
        [Test]
        public void CallsPropertyValueSerializerToReadAllPropValues()
        {
            var propSerializer = new Mock<IPropertyValueSerializer>();
            propSerializer.Setup(p => p.Serialize(It.IsAny<IEnumerable<object>>())).Returns(new StringBuilder());
            var fileService = new Mock<IFileWriterService>();
            
            var fileWriter = new FlatFileItemWriter<string>(propSerializer.Object, fileService.Object);

            var values = new[] { "one", "two" };

            fileWriter.Write(values);

            propSerializer.Verify(p => p.Serialize(values));
        }

        [Test]
        public void CallsFileServiceToWriterItemsToFile()
        {
            var strBuilder = new StringBuilder("hello");
            var propSerializer = new Mock<IPropertyValueSerializer>();
            propSerializer.Setup(p => p.Serialize(It.IsAny<IEnumerable<object>>())).Returns(strBuilder);

            var fileService = new Mock<IFileWriterService>();
            var fileWriter = new FlatFileItemWriter<string>(propSerializer.Object, fileService.Object);

            fileWriter.Write(new[] { "one" });

            fileService.Verify(f => f.Write(strBuilder.ToString()));
        }
    }
}
