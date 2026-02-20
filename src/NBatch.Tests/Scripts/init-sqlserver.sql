-- =============================================================================
-- Integration test seed data for SQL Server.
-- Creates the NBatch_IntegrationTests database with a TestRecord table
-- containing 50,000 deterministic rows and an empty TestRecordEtl table
-- for cross-database ETL tests.
--
-- Executed automatically by docker-compose on first start.
-- =============================================================================

IF DB_ID('NBatch_IntegrationTests') IS NULL
    CREATE DATABASE NBatch_IntegrationTests;
GO

USE NBatch_IntegrationTests;
GO

-- Source table: 50,000 deterministic test records
IF OBJECT_ID('TestRecord', 'U') IS NULL
CREATE TABLE TestRecord
(
    Id       INT            NOT NULL IDENTITY(1,1) PRIMARY KEY,
    Code     NVARCHAR(50)   NOT NULL,
    Value    DECIMAL(18, 2) NOT NULL,
    Category NVARCHAR(50)   NOT NULL
);
GO

-- ETL destination table (populated by cross-database tests, cleaned between runs)
IF OBJECT_ID('TestRecordEtl', 'U') IS NULL
CREATE TABLE TestRecordEtl
(
    Id       INT            NOT NULL IDENTITY(1,1) PRIMARY KEY,
    Code     NVARCHAR(50)   NOT NULL,
    Value    DECIMAL(18, 2) NOT NULL,
    Category NVARCHAR(50)   NOT NULL
);
GO

-- Seed 50,000 rows if the table is empty.
-- 5 categories × 10,000 rows each for filtered-read tests.
IF NOT EXISTS (SELECT 1 FROM TestRecord)
BEGIN
    ;WITH Numbers AS (
        SELECT 1 AS n
        UNION ALL
        SELECT n + 1 FROM Numbers WHERE n < 50000
    )
    INSERT INTO TestRecord (Code, Value, Category)
    SELECT
        'REC-' + RIGHT('00000' + CAST(n AS VARCHAR), 5),
        CAST(n * 1.23 AS DECIMAL(18, 2)),
        CASE (n % 5)
            WHEN 0 THEN 'Alpha'
            WHEN 1 THEN 'Beta'
            WHEN 2 THEN 'Gamma'
            WHEN 3 THEN 'Delta'
            WHEN 4 THEN 'Epsilon'
        END
    FROM Numbers
    OPTION (MAXRECURSION 0);
END
GO
