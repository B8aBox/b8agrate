IF
DB_ID(N'b8agrate') IS NULL
BEGIN
    CREATE
DATABASE [b8agrate];
END
GO

USE [b8agrate]
GO

/*************************************************************************
CONFIGURE SECURITY
*************************************************************************/

IF NOT EXISTS (SELECT * FROM [master].[sys].[server_principals] WHERE [name] = 'b8agrate_migration')
    CREATE
LOGIN [b8agrate_migration] WITH PASSWORD = N'1Secure*Password1', DEFAULT_DATABASE = [b8agrate], DEFAULT_LANGUAGE = [us_english], CHECK_EXPIRATION = OFF, CHECK_POLICY = ON
GO

IF NOT EXISTS (SELECT * FROM [sys].[database_principals] WHERE name = 'b8agrate_migration')
    CREATE
USER [b8agrate_migration] FOR LOGIN [b8agrate_migration] WITH DEFAULT_SCHEMA = [dbo]
GO

ALTER
ROLE [db_owner] ADD MEMBER [b8agrate_migration]
GO

IF NOT EXISTS (SELECT * FROM [master].[sys].[server_principals] WHERE [name] = 'b8agrate_app')
    CREATE
LOGIN [b8agrate_app] WITH PASSWORD = N'1Secure*Password1', DEFAULT_DATABASE = [b8agrate], DEFAULT_LANGUAGE = [us_english], CHECK_EXPIRATION = OFF, CHECK_POLICY = ON
GO

IF NOT EXISTS (SELECT * FROM [sys].[database_principals] WHERE name = 'b8agrate_app')
    CREATE
USER [b8agrate_app] FOR LOGIN [b8agrate_app] WITH DEFAULT_SCHEMA = [dbo]
GO

ALTER
ROLE [db_datareader] ADD MEMBER [b8agrate_app]
GO

ALTER
ROLE [db_datawriter] ADD MEMBER [b8agrate_app]
GO