using NBatch.Main.Readers.SqlReader;
using NUnit.Framework;

namespace NBatch.Main.UnitTests.Readers.SqlReader
{
    [TestFixture]
    class SqlDbItemReaderTests
    {
        [Test]
        public void WriteSomeAwesomeTest()
        {
            var fakeDb = new FakeDb();

            var sqlReader = new SqlDbItemReader<string>(fakeDb);

            sqlReader.Read(0, 10);

            Assert.That(fakeDb.ExecuteCalled, Is.True);
        }
    }
}