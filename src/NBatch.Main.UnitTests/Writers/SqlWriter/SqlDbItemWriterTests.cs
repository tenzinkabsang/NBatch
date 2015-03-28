using NBatch.Main.Writers.SqlWriter;
using NUnit.Framework;

namespace NBatch.Main.UnitTests.Writers.SqlWriter
{
    [TestFixture]
    class SqlDbItemWriterTests
    {
        [Test]
        public void CallsDbExecuteQuery()
        {
            var fakeDb = new FakeDb();

            var sqlWriter = new SqlDbItemWriter<string>(fakeDb);

            sqlWriter.Write(null);

            Assert.That(fakeDb.ExecuteCalled, Is.True);
        }
    }
}
