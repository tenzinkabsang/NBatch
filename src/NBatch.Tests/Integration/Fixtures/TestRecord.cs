namespace NBatch.Tests.Integration.Fixtures;

/// <summary>
/// Source entity mapped to the TestRecord table seeded with 50,000 rows
/// by the init SQL scripts.
/// </summary>
internal sealed class TestRecord
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public decimal Value { get; set; }
    public string Category { get; set; } = "";
}

/// <summary>
/// Destination entity for cross-database ETL tests.
/// Mapped to the TestRecordEtl table (created empty by init scripts).
/// </summary>
internal sealed class TestRecordEtl
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public decimal Value { get; set; }
    public string Category { get; set; } = "";
}
