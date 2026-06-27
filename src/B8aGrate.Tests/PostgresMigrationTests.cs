using B8aGrate.Application.Features.ApplyMigrations;
using B8aGrate.Application.Features.GetMigrationSnapshot;
using B8aGrate.Application.Features.UndoMigrations;
using B8aGrate.Application.Features.UndoProvisioning;
using B8aGrate.Data.Services;
using B8aGrate.Data.Sql.Repositories;
using B8aGrate.Domain.ValueObjects;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace B8aGrate.Tests;

public sealed class PostgresMigrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder("postgres:16-alpine").Build();

    public Task InitializeAsync() => _db.StartAsync();
    public Task DisposeAsync() => _db.DisposeAsync().AsTask();

    [Fact]
    public async Task Migrate_AdoptValidate_And_Snapshot()
    {
        var root = CreateTempRoot(ProviderType.PostgreSql, ("V000001__create_widgets.sql", "CREATE TABLE public.widgets (id INT PRIMARY KEY);"));
        var repositoryFactory = new MigrationRepositoryFactory();
        var discoverer = new MigrationScriptDiscoverer();

        var migrate = await new ApplyMigrationsCommandHandler(discoverer, repositoryFactory).Handle(new ApplyMigrationsCommand
        {
            ConnectionString = _db.GetConnectionString(),
            Root = root
        }, CancellationToken.None);

        Assert.True(migrate.IsValid, string.Join(Environment.NewLine, migrate.Detail.Select(x => x.Message)));

        var snapshot = await new GetMigrationSnapshotQueryHandler(discoverer, repositoryFactory).Handle(new GetMigrationSnapshotQuery
        {
            ConnectionString = _db.GetConnectionString(),
            Root = root
        }, CancellationToken.None);

        Assert.True(snapshot.IsValid, string.Join(Environment.NewLine, snapshot.Detail.Select(x => x.Message)));
        Assert.Contains(snapshot.Content!.AppliedMigrations, x => x.Version == "000001");
    }

    [Fact]
    public async Task Migrate_And_Undo_Roundtrip()
    {
        var root = CreateTempRoot(ProviderType.PostgreSql,
            ("V000001__create_widgets.sql", "CREATE TABLE public.widgets_roundtrip (id INT PRIMARY KEY);"),
            ("U000001__drop_widgets.sql", "DROP TABLE public.widgets_roundtrip;"));

        var repositoryFactory = new MigrationRepositoryFactory();
        var discoverer = new MigrationScriptDiscoverer();

        var migrate = await new ApplyMigrationsCommandHandler(discoverer, repositoryFactory).Handle(new ApplyMigrationsCommand
        {
            ConnectionString = _db.GetConnectionString(),
            Root = root
        }, CancellationToken.None);

        Assert.True(migrate.IsValid, string.Join(Environment.NewLine, migrate.Detail.Select(x => x.Message)));
        Assert.True(await ExistsAsync(_db.GetConnectionString(), "widgets_roundtrip"));

        var undo = await new UndoMigrationsCommandHandler(discoverer, repositoryFactory).Handle(new UndoMigrationsCommand
        {
            ConnectionString = _db.GetConnectionString(),
            Root = root,
            Steps = 1
        }, CancellationToken.None);

        Assert.True(undo.IsValid, string.Join(Environment.NewLine, undo.Detail.Select(x => x.Message)));
        Assert.False(await ExistsAsync(_db.GetConnectionString(), "widgets_roundtrip"));
    }

    [Fact]
    public async Task Migrate_With_Provision_Creates_Target_And_Records_Provision()
    {
        var databaseName = CreateDatabaseName();
        var adminConnectionString = _db.GetConnectionString();
        var targetConnectionString = WithDatabase(adminConnectionString, databaseName);

        var root = CreateTempRoot(ProviderType.PostgreSql,
            ("P__create_database.sql", $"""CREATE DATABASE "{databaseName}";"""),
            ("V000001__create_widgets.sql", "CREATE TABLE public.widgets (id INT PRIMARY KEY);"));

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

        var root = CreateTempRoot(ProviderType.PostgreSql,
            ("P__create_database.sql", $"""CREATE DATABASE "{databaseName}";"""),
            ("PU__drop_database.sql", $"""DROP DATABASE "{databaseName}" WITH (FORCE);"""));

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
        var root = CreateTempRoot(ProviderType.PostgreSql,
            ("V000001__create_widgets.sql", "CREATE TABLE public.widgets_without_provision (id INT PRIMARY KEY);"),
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
        await using var cn = new NpgsqlConnection(connectionString);
        await cn.OpenAsync();
        await using var cmd = new NpgsqlCommand("SELECT COUNT(1) FROM information_schema.tables WHERE table_schema = 'public' AND table_name = @name", cn);
        cmd.Parameters.AddWithValue("name", table);

        return Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
    }

    private static string CreateDatabaseName() => $"b8agrate_{Guid.NewGuid():N}";

    private static async Task<bool> DatabaseExistsAsync(string connectionString, string databaseName)
    {
        await using var cn = new NpgsqlConnection(connectionString);
        await cn.OpenAsync();
        await using var cmd = new NpgsqlCommand("SELECT COUNT(1) FROM pg_database WHERE datname = @name", cn);
        cmd.Parameters.AddWithValue("name", databaseName);

        return Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
    }

    private static async Task DropDatabaseIfExistsAsync(string connectionString, string databaseName)
    {
        await using var cn = new NpgsqlConnection(connectionString);
        await cn.OpenAsync();
        await using var cmd = new NpgsqlCommand($"""DROP DATABASE IF EXISTS "{databaseName}" WITH (FORCE);""", cn);

        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task<bool> MigrationKindExistsAsync(string connectionString, string kind)
    {
        await using var cn = new NpgsqlConnection(connectionString);
        await cn.OpenAsync();
        await using var cmd = new NpgsqlCommand("""SELECT COUNT(1) FROM b8agrate."Migration" WHERE "Kind" = @kind""", cn);
        cmd.Parameters.AddWithValue("kind", kind);

        return Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
    }

    private static string WithDatabase(string connectionString, string databaseName)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString)
        {
            Database = databaseName
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