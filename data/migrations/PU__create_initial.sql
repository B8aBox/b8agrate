IF
DB_ID(N'b8agrate') IS NOT NULL
BEGIN
    USE
[b8agrate];

    DECLARE
@transferSchemaOwnershipSql NVARCHAR(MAX) = N'';

SELECT @transferSchemaOwnershipSql +=
        N'ALTER AUTHORIZATION ON SCHEMA::' + QUOTENAME(s.name) + N' TO [dbo];' + CHAR(13)
FROM sys.schemas s
    INNER JOIN sys.database_principals p
ON p.principal_id = s.principal_id
WHERE p.name IN (N'b8agrate_app', N'b8agrate_migration');

IF
@transferSchemaOwnershipSql <> N''
        EXEC sys.sp_executesql @transferSchemaOwnershipSql;

    IF
EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'b8agrate_app')
BEGIN
        IF
IS_ROLEMEMBER(N'db_datawriter', N'b8agrate_app') = 1
            ALTER
ROLE [db_datawriter] DROP
MEMBER [b8agrate_app];

        IF
IS_ROLEMEMBER(N'db_datareader', N'b8agrate_app') = 1
            ALTER
ROLE [db_datareader] DROP
MEMBER [b8agrate_app];

        DROP
USER [b8agrate_app];
END

    IF
EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'b8agrate_migration')
BEGIN
        IF
IS_ROLEMEMBER(N'db_owner', N'b8agrate_migration') = 1
            ALTER
ROLE [db_owner] DROP
MEMBER [b8agrate_migration];

        DROP
USER [b8agrate_migration];
END
END
GO

USE [master]
GO

IF DB_ID(N'b8agrate') IS NOT NULL
BEGIN
    ALTER
DATABASE [b8agrate] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP
DATABASE [b8agrate];
END
GO

IF EXISTS (SELECT 1 FROM [master].[sys].[server_principals] WHERE [name] = N'b8agrate_app')
    DROP
LOGIN [b8agrate_app];
GO

IF EXISTS (SELECT 1 FROM [master].[sys].[server_principals] WHERE [name] = N'b8agrate_migration')
    DROP
LOGIN [b8agrate_migration];
GO
