using NBatch.Main.Readers.SqlReader;
using NUnit.Framework;

namespace NBatch.Main.UnitTests.Readers.SqlReader
{
    [TestFixture]
    class SqlDbItemReaderTests
    {
        [Test]
        public void DbIsCalledForActualExecution()
        {
            var fakeDb = new FakeDb();

            var sqlReader = new SqlDbItemReader<string>(fakeDb)
                .Query("query")
                .OrderBy("foo");

            sqlReader.Read(0, 10);

            Assert.That(fakeDb.ExecuteCalled, Is.True);
        }

        [Test]
        public void OrderByClauseMustBeSet()
        {
            var sqlReader = new SqlDbItemReader<string>(new FakeDb());
            Assert.That(() => sqlReader.Read(0, 10), Throws.Exception.Message.Contains("order by clause"));
        }
    }
}