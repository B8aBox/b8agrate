using B8aGrate.Application.Features.ApplyMigrations;
using B8aGrate.Application.Features.UndoMigrations;
using B8aGrate.Application.Features.UndoProvisioning;
using B8aGrate.Data.Services;
using B8aGrate.Data.Sql.Repositories;
using B8aGrate.Domain.ValueObjects;
using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;
using Xunit;

namespace B8aGrate.Tests;

public sealed class SqlServerMigrationTests : IAsyncLifetime
{
    private readonly MsSqlContainer _db = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    public Task InitializeAsync() => _db.StartAsync();
    public Task DisposeAsync() => _db.DisposeAsync().AsTask();

    [Fact]
    public async Task Migrate_And_Undo_Roundtrip()
    {
        var root = CreateTempRoot(ProviderType.SqlServer,
            ("V000001__create_widgets.sql", "CREATE TABLE dbo.widgets (id INT NOT NULL PRIMARY KEY);"),
            ("U000001__drop_widgets.sql", "DROP TABLE dbo.widgets;"));

        var repositoryFactory = new MigrationRepositoryFactory();
        var discoverer = new MigrationScriptDiscoverer();

        var migrate = await new ApplyMigrationsCommandHandler(discoverer, repositoryFactory).Handle(new ApplyMigrationsCommand
        {
            ConnectionString = _db.GetConnectionString(),
            Root = root
        }, CancellationToken.None);

        Assert.True(migrate.IsValid, string.Join(Environment.NewLine, migrate.Detail.Select(x => x.Message)));
        Assert.True(await ExistsAsync(_db.GetConnectionString(), "widgets"));

        var undo = await new UndoMigrationsCommandHandler(discoverer, repositoryFactory).Handle(new UndoMigrationsCommand
        {
            ConnectionString = _db.GetConnectionString(),
            Root = root,
            Steps = 1
        }, CancellationToken.None);

        Assert.True(undo.IsValid, string.Join(Environment.NewLine, undo.Detail.Select(x => x.Message)));
        Assert.False(await ExistsAsync(_db.GetConnectionString(), "widgets"));
    }

    [Fact]
    public async Task SqlServer_Lock_Releases_After_Script_Changes_Database_Context()
    {
        var repository = new SqlServerMigrationRepository(_db.GetConnectionString(), null, "b8agrate", "Migration", 30);

        await using var connection = await repository.OpenConnection(CancellationToken.None);
        using var migrationLock = await repository.AcquireLock(connection, CancellationToken.None);

        await using var command = new SqlCommand("USE tempdb;", (SqlConnection)connection);

        await command.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task Migrate_With_Provision_Creates_Target_And_Records_Provision()
    {
        var databaseName = CreateDatabaseName();
        var adminConnectionString = _db.GetConnectionString();
        var targetConnectionString = WithDatabase(adminConnectionString, databaseName);

        var root = CreateTempRoot(ProviderType.SqlServer,
            ("P__create_database.sql", $"CREATE DATABASE [{databaseName}];"),
            ("V000001__create_widgets.sql", "CREATE TABLE dbo.widgets (id INT NOT NULL PRIMARY KEY);"));

        try
        {
            var repositoryFactory = new MigrationRepositoryFactory();
            var discoverer = new MigrationScriptDiscoverer();

            var migrate = await new ApplyMigrationsCommandHandler(discoverer, repositoryFactory).Handle(new ApplyMigrationsCommand
            {
                AdminConnectionString = adminConnectionString,
                ConnectionString = targetConnectionString,
                Root = root
            }, CancellationToken.None);

            Assert.True(migrate.IsValid, string.Join(Environment.NewLine, migrate.Detail.Select(x => x.Message)));
            Assert.True(await DatabaseExistsAsync(adminConnectionString, databaseName));
            Assert.True(await ExistsAsync(targetConnectionString, "widgets"));
            Assert.True(await MigrationKindExistsAsync(targetConnectionString, "Provision"));
        }
        finally
        {
            await DropDatabaseIfExistsAsync(adminConnectionString, databaseName);
        }
    }

    [Fact]
    public async Task Unprovision_Drops_Target_And_Returns_Success_When_History_Cannot_Be_Recorded()
    {
        var databaseName = CreateDatabaseName();
        var adminConnectionString = _db.GetConnectionString();
        var targetConnectionString = WithDatabase(adminConnectionString, databaseName);

        var root = CreateTempRoot(ProviderType.SqlServer,
            ("P__create_database.sql", $"CREATE DATABASE [{databaseName}];"),
            ("PU__drop_database.sql", $"""
                                       ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                                       GO
                                       DROP DATABASE [{databaseName}];
                                       """));

        try
        {
            var repositoryFactory = new MigrationRepositoryFactory();
            var discoverer = new MigrationScriptDiscoverer();

            var migrate = await new ApplyMigrationsCommandHandler(discoverer, repositoryFactory).Handle(new ApplyMigrationsCommand
            {
                AdminConnectionString = adminConnectionString,
                ConnectionString = targetConnectionString,
                Root = root
            }, CancellationToken.None);

            Assert.True(migrate.IsValid, string.Join(Environment.NewLine, migrate.Detail.Select(x => x.Message)));

            var unprovision = await new UndoProvisioningCommandHandler(discoverer, repositoryFactory).Handle(new UndoProvisioningCommand
            {
                AdminConnectionString = adminConnectionString,
                ConnectionString = targetConnectionString,
                Root = root
            }, CancellationToken.None);

            Assert.True(unprovision.IsValid, string.Join(Environment.NewLine, unprovision.Detail.Select(x => x.Message)));
            Assert.Contains("could not be opened afterward", unprovision.Content!.Message);
            Assert.False(await DatabaseExistsAsync(adminConnectionString, databaseName));
        }
        finally
        {
            await DropDatabaseIfExistsAsync(adminConnectionString, databaseName);
        }
    }

    [Fact]
    public async Task Unprovision_Without_Applied_Provision_Returns_Invalid()
    {
        var root = CreateTempRoot(ProviderType.SqlServer,
            ("V000001__create_widgets.sql", "CREATE TABLE dbo.widgets_without_provision (id INT NOT NULL PRIMARY KEY);"),
            ("PU__drop_database.sql", "SELECT 1;"));

        var repositoryFactory = new MigrationRepositoryFactory();
        var discoverer = new MigrationScriptDiscoverer();

        var migrate = await new ApplyMigrationsCommandHandler(discoverer, repositoryFactory).Handle(new ApplyMigrationsCommand
        {
            ConnectionString = _db.GetConnectionString(),
            Root = root
        }, CancellationToken.None);

        Assert.True(migrate.IsValid, string.Join(Environment.NewLine, migrate.Detail.Select(x => x.Message)));

        var unprovision = await new UndoProvisioningCommandHandler(discoverer, repositoryFactory).Handle(new UndoProvisioningCommand
        {
            AdminConnectionString = _db.GetConnectionString(),
            ConnectionString = _db.GetConnectionString(),
            Root = root
        }, CancellationToken.None);

        Assert.False(unprovision.IsValid);
        Assert.Contains(unprovision.Detail, x => x.Message == "No applied provisioning record was found.");
    }

    private static async Task<bool> ExistsAsync(string connectionString, string table)
    {
        await using var cn = new SqlConnection(connectionString);
        await cn.OpenAsync();
        await using var cmd = new SqlCommand("SELECT COUNT(1) FROM sys.objects WHERE name = @name AND type = 'U'", cn);
        cmd.Parameters.AddWithValue("name", table);

        return Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
    }

    private static string CreateDatabaseName() => $"b8agrate_{Guid.NewGuid():N}";

    private static async Task<bool> DatabaseExistsAsync(string connectionString, string databaseName)
    {
        await using var cn = new SqlConnection(connectionString);
        await cn.OpenAsync();
        await using var cmd = new SqlCommand("SELECT DB_ID(@name)", cn);
        cmd.Parameters.AddWithValue("name", databaseName);

        return await cmd.ExecuteScalarAsync() is not DBNull and not null;
    }

    private static async Task DropDatabaseIfExistsAsync(string connectionString, string databaseName)
    {
        await using var cn = new SqlConnection(connectionString);
        await cn.OpenAsync();

        await using var cmd = new SqlCommand($"""
                                              IF DB_ID(@name) IS NOT NULL
                                              BEGIN
                                                  DECLARE @sql nvarchar(max) = N'ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}];';
                                                  EXEC(@sql);
                                              END
                                              """, cn);

        cmd.Parameters.AddWithValue("name", databaseName);

        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task<bool> MigrationKindExistsAsync(string connectionString, string kind)
    {
        await using var cn = new SqlConnection(connectionString);
        await cn.OpenAsync();
        await using var cmd = new SqlCommand("SELECT COUNT(1) FROM b8agrate.Migration WHERE Kind = @kind", cn);
        cmd.Parameters.AddWithValue("kind", kind);

        return Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
    }

    private static string WithDatabase(string connectionString, string databaseName)
    {
        var builder = new SqlConnectionStringBuilder(connectionString)
        {
            InitialCatalog = databaseName
        };

        return builder.ConnectionString;
    }

    private static string CreateTempRoot(ProviderType provider, params (string Name, string Sql)[] files)
    {
        var root = Path.Combine(Path.GetTempPath(), "b8agrate-tests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(root, "migrations");
        Directory.CreateDirectory(path);

        var configuration = new Configuration { Provider = provider };
        configuration.Migration.Path = "migrations";

        File.WriteAllText(Configuration.GetPath(root), configuration.ToJson());

        foreach (var file in files)
            File.WriteAllText(Path.Combine(path, file.Name), file.Sql);

        return root;
    }
}