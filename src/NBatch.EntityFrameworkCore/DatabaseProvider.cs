namespace NBatch.Core;

/// <summary>
/// Specifies the database provider used for NBatch job tracking.
/// </summary>
public enum DatabaseProvider
{
    /// <summary>Microsoft SQL Server.</summary>
    SqlServer,
    /// <summary>PostgreSQL via Npgsql.</summary>
    PostgreSql,
    /// <summary>SQLite.</summary>
    Sqlite,
    /// <summary>MySQL / MariaDB via Pomelo.</summary>
    MySql
}
