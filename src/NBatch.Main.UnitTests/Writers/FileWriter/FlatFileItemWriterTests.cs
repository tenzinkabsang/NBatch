using System.Collections.Generic;
using System.Linq;
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
            var fileService = new Mock<IFileWriterService>();
            
            var fileWriter = new FlatFileItemWriter<string>(propSerializer.Object, fileService.Object);

            var values = new[] { "one", "two" };

            bool isSaved = fileWriter.Write(values);

            Assert.IsFalse(isSaved);
            propSerializer.Verify(p => p.Serialize(values));
            fileService.Verify(f => f.WriteFile(It.IsAny<IEnumerable<string>>()), Times.Never());
        }

        [Test]
        public void CallsFileServiceToWriterItemsToFile()
        {
            var values = new[] {"hello"};
            var propSerializer = new Mock<IPropertyValueSerializer>();
            propSerializer.Setup(p => p.Serialize(It.IsAny<IEnumerable<object>>())).Returns(values);

            var fileService = new Mock<IFileWriterService>();
            var fileWriter = new FlatFileItemWriter<string>(propSerializer.Object, fileService.Object);

            bool isSaved = fileWriter.Write(new[] { "one" });

            Assert.IsTrue(isSaved);
            fileService.Verify(f => f.WriteFile(values));
        }
    }
}
