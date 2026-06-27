CREATE TABLE dbo.Country
(
    Code      CHAR(2)       NOT NULL
        CONSTRAINT PK_Country PRIMARY KEY,
    Name      NVARCHAR(100) NOT NULL,
    CreatedAt DATETIME2        NOT NULL
        CONSTRAINT DF_Country_CreatedAt DEFAULT SYSUTCDATETIME()
);
GO
