-- =============================================================================
-- Product table & seed data for the NBatch console app samples.
-- NBatch tracking tables (BatchJob, BatchStep, etc.) are created automatically
-- by EfJobRepository via EnsureCreatedAsync — no script needed for those.
--
-- Run against SQL Server after docker-compose up:
--   sqlcmd -S localhost -U sa -P @Password1234 -i init-db.sql
-- =============================================================================

IF DB_ID('NBatch') IS NULL
    CREATE DATABASE NBatch;
GO

USE NBatch;
GO

IF OBJECT_ID('Product', 'U') IS NULL
CREATE TABLE Product
(
    Id          INT             NOT NULL IDENTITY(1,1) PRIMARY KEY,
    Sku         NVARCHAR(50)    NOT NULL,
    Name        NVARCHAR(200)   NOT NULL,
    Description NVARCHAR(500)   NOT NULL,
    Price       DECIMAL(18, 2)  NOT NULL
);
GO


IF OBJECT_ID('ProductLowercase', 'U') IS NULL
CREATE TABLE ProductLowercase
(
    Id          INT             NOT NULL IDENTITY(1,1) PRIMARY KEY,
    Sku         NVARCHAR(50)    NOT NULL,
    Name        NVARCHAR(200)   NOT NULL,
    Description NVARCHAR(500)   NOT NULL,
    Price       DECIMAL(18, 2)  NOT NULL
);
GO

-- Seed only if the table is empty
IF NOT EXISTS (SELECT 1 FROM Product)
BEGIN
    INSERT INTO Product (Sku, Name, Description, Price) VALUES
        ('1111', 'C# For Dummies',        'The book you should avoid',                800.00),
        ('2222', 'Design Patterns',        'The worlds authority on software designs',  299.99),
        ('3333', 'Java 8 In Depth',        'Finally Lambdas',                          399.99),
        ('4444', 'Effective Java',          'The best java book',                       499.99),
        ('5555', 'ASP.NET MVC 9',           'Not even Model View Controller',           599.99),
        ('6666', 'Web API',                 'Easy way to expose endpoints',             699.99),
        ('7777', 'C++ In-Depth',            'Everything related to C++',                799.99),
        ('8888', 'Scala Programming',       'Beautiful functional',                     899.99),
        ('9999', 'Reactive Extensions',     'Asynchrony at its best',                   999.99);
END
GO

