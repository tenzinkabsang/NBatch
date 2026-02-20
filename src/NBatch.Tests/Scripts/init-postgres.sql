-- =============================================================================
-- Integration test seed data for PostgreSQL.
-- Creates a TestRecord table with 50,000 deterministic rows and an empty
-- TestRecordEtl table for cross-database ETL tests.
--
-- Executed automatically by docker-entrypoint-initdb.d on first start.
-- Target database: nbatch_integration (created by POSTGRES_DB env var).
-- =============================================================================

-- Source table: 50,000 deterministic test records
CREATE TABLE IF NOT EXISTS "TestRecord"
(
    "Id"       SERIAL         PRIMARY KEY,
    "Code"     VARCHAR(50)    NOT NULL,
    "Value"    DECIMAL(18, 2) NOT NULL,
    "Category" VARCHAR(50)    NOT NULL
);

-- ETL destination table (populated by cross-database tests, cleaned between runs)
CREATE TABLE IF NOT EXISTS "TestRecordEtl"
(
    "Id"       SERIAL         PRIMARY KEY,
    "Code"     VARCHAR(50)    NOT NULL,
    "Value"    DECIMAL(18, 2) NOT NULL,
    "Category" VARCHAR(50)    NOT NULL
);

-- Seed 50,000 rows if the table is empty.
-- 5 categories × 10,000 rows each for filtered-read tests.
INSERT INTO "TestRecord" ("Code", "Value", "Category")
SELECT
    'REC-' || LPAD(n::TEXT, 5, '0'),
    ROUND((n * 1.23)::NUMERIC, 2),
    CASE (n % 5)
        WHEN 0 THEN 'Alpha'
        WHEN 1 THEN 'Beta'
        WHEN 2 THEN 'Gamma'
        WHEN 3 THEN 'Delta'
        WHEN 4 THEN 'Epsilon'
    END
FROM generate_series(1, 50000) AS s(n)
WHERE NOT EXISTS (SELECT 1 FROM "TestRecord" LIMIT 1);
