using B8aGrate.Application.Extensions;
using B8aGrate.Data.Services;
using B8aGrate.Domain.ValueObjects;
using Xunit;

namespace B8aGrate.Tests;

public sealed class MigrationDiscoveryTests
{
    private readonly MigrationScriptDiscoverer _discoverer = new();

    [Fact]
    public void Discover_Recognizes_Provision_And_ProvisionUndo_Scripts()
    {
        var path = CreateTempMigrations(
            ("P__create_database.sql", "-- provision"),
            ("PU__Create__Initial.sql", "-- provision undo"),
            ("V000001__create_schema.sql", "-- versioned"));

        var scripts = _discoverer.Discover(path);

        Assert.Contains(scripts, x => x.Kind == MigrationKind.Provision && x.Version is null);
        Assert.Contains(scripts, x => x.Kind == MigrationKind.ProvisionUndo && x.Version is null && x.Description == "Create Initial");
        Assert.Contains(scripts, x => x.Kind == MigrationKind.Versioned && x.Version == "000001");
    }

    [Fact]
    public void Discover_Allows_Provision_And_Versioned_Scripts()
    {
        var path = CreateTempMigrations(
            ("P__create_database.sql", "-- provision"),
            ("V000001__create_schema.sql", "-- versioned"));

        var scripts = _discoverer.Discover(path);

        Assert.Equal(2, scripts.Count);
    }

    [Fact]
    public void Discover_Reports_Multiple_Provision_Scripts_As_Discovery_Errors()
    {
        var path = CreateTempMigrations(
            ("P__create_database.sql", "-- provision"),
            ("P__create_users.sql", "-- users"));

        var scripts = _discoverer.Discover(path);
        var errors = scripts.GetDiscoveryErrors();

        Assert.Contains(errors, x => x.Code == "MultipleProvisionScripts");
    }

    [Fact]
    public void Discover_Ignores_Legacy_Versioned_Provision_Scripts()
    {
        var path = CreateTempMigrations(
            ("P000001__create_database.sql", "-- provision"),
            ("PU000001__drop_database.sql", "-- provision undo"));

        var scripts = _discoverer.Discover(path);

        Assert.Empty(scripts);
    }

    [Fact]
    public void Discover_Only_Reads_Top_Level_Migration_Files()
    {
        var path = CreateTempMigrations(("V000001__create_schema.sql", "-- versioned"));
        var nested = Path.Combine(path, "nested");
        Directory.CreateDirectory(nested);
        File.WriteAllText(Path.Combine(nested, "V000002__ignored.sql"), "-- ignored");

        var scripts = _discoverer.Discover(path);

        Assert.Single(scripts);
        Assert.Contains(scripts, x => x.Version == "000001");
    }

    private static string CreateTempMigrations(params (string Name, string Sql)[] files)
    {
        var path = Path.Combine(Path.GetTempPath(), "b8agrate-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);

        foreach (var file in files)
            File.WriteAllText(Path.Combine(path, file.Name), file.Sql);

        return path;
    }
}