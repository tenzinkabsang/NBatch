using System.Globalization;
using Moq;
using NBatch.Readers.FileReader;
using NBatch.Readers.FileReader.Services;
using NUnit.Framework;

namespace NBatch.Tests.Readers.FileReaders;

[TestFixture]
internal sealed class CsvRowTests
{
    private static async Task<CsvRow> RowFromAsync(string header, string dataLine)
    {
        CsvRow? captured = null;
        var fileService = new Mock<IFileService>();
        fileService.Setup(f => f.ReadLinesAsync(0, 1, It.IsAny<CancellationToken>()))
            .Returns(new[] { new CsvLine(1, header) }.ToAsyncEnumerable());
        fileService.Setup(f => f.ReadLinesAsync(1, 1, It.IsAny<CancellationToken>()))
            .Returns(new[] { new CsvLine(2, dataLine) }.ToAsyncEnumerable());

        var reader = new CsvReader<int>("fake.csv", row => { captured = row; return 0; }, fileService.Object);
        _ = (await reader.ReadAsync(0, 1)).ToList();
        return captured!;
    }

    [Test]
    public async Task GetDateTime_and_GetGuid_parse_by_name_and_index()
    {
        var id = Guid.NewGuid();
        var row = await RowFromAsync("When,Id", $"2030-06-15T10:30:00,{id}");

        Assert.That(row.GetDateTime("When"), Is.EqualTo(new DateTime(2030, 6, 15, 10, 30, 0)));
        Assert.That(row.GetGuid("Id"), Is.EqualTo(id));
    }

    [Test]
    public async Task OrNull_accessors_return_null_for_missing_column()
    {
        var row = await RowFromAsync("Name", "Widget");

        Assert.That(row.GetStringOrNull("Missing"), Is.Null);
        Assert.That(row.GetIntOrNull("Missing"), Is.Null);
        Assert.That(row.GetLongOrNull("Missing"), Is.Null);
        Assert.That(row.GetDecimalOrNull("Missing"), Is.Null);
        Assert.That(row.GetDoubleOrNull("Missing"), Is.Null);
        Assert.That(row.GetBoolOrNull("Missing"), Is.Null);
        Assert.That(row.GetDateTimeOrNull("Missing"), Is.Null);
        Assert.That(row.GetGuidOrNull("Missing"), Is.Null);
    }

    [Test]
    public async Task OrNull_accessors_return_null_for_empty_value()
    {
        var row = await RowFromAsync("Name,Count", "Widget,");

        Assert.That(row.GetStringOrNull("Count"), Is.Null);
        Assert.That(row.GetIntOrNull("Count"), Is.Null);
    }

    [Test]
    public async Task OrNull_accessors_parse_non_empty_values()
    {
        var id = Guid.NewGuid();
        var row = await RowFromAsync("A,B,C,D,E,F,G",
            $"5,9000000000,19.99,1.5,true,2030-01-02,{id}");

        Assert.That(row.GetIntOrNull("A"), Is.EqualTo(5));
        Assert.That(row.GetLongOrNull("B"), Is.EqualTo(9_000_000_000L));
        Assert.That(row.GetDecimalOrNull("C"), Is.EqualTo(19.99m));
        Assert.That(row.GetDoubleOrNull("D"), Is.EqualTo(1.5));
        Assert.That(row.GetBoolOrNull("E"), Is.True);
        Assert.That(row.GetDateTimeOrNull("F"), Is.EqualTo(new DateTime(2030, 1, 2)));
        Assert.That(row.GetGuidOrNull("G"), Is.EqualTo(id));
    }

    [Test]
    public async Task OrNull_accessors_still_throw_for_unparseable_values()
    {
        var row = await RowFromAsync("Count", "not-a-number");

        Assert.Throws<FormatException>(() => row.GetIntOrNull("Count"));
    }

    [Test]
    public async Task Existing_accessors_parse_with_invariant_culture()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            var row = await RowFromAsync("Price", "19.99");

            Assert.That(row.GetDecimal("Price"), Is.EqualTo(19.99m));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }
}
