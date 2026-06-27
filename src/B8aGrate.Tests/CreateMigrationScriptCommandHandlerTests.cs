using B8aGrate.Application.Features.CreateMigrationScript;
using B8aGrate.Data.Services.Interfaces;
using B8aGrate.Domain.ValueObjects;
using Xunit;

namespace B8aGrate.Tests;

public sealed class CreateMigrationScriptCommandHandlerTests
{
    [Fact]
    public async Task Handle_Creates_Provision_And_ProvisionUndo_Scripts()
    {
        var root = CreateProjectRoot();
        var handler = new CreateMigrationScriptCommandHandler(new StubMigrationVersionProvider());

        var result = await handler.Handle(new CreateMigrationScriptCommand
        {
            HasUndo = true,
            IsProvision = true,
            Name = "create database",
            Root = root
        }, CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.True(File.Exists(Path.Combine(root, "data", "migrations", "P__create_database.sql")));
        Assert.True(File.Exists(Path.Combine(root, "data", "migrations", "PU__create_database.sql")));
    }

    [Fact]
    public async Task Handle_Creates_Only_Provision_When_Undo_Is_Disabled()
    {
        var root = CreateProjectRoot();
        var handler = new CreateMigrationScriptCommandHandler(new StubMigrationVersionProvider());

        var result = await handler.Handle(new CreateMigrationScriptCommand
        {
            HasUndo = false,
            IsProvision = true,
            Name = "create database",
            Root = root
        }, CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.True(File.Exists(Path.Combine(root, "data", "migrations", "P__create_database.sql")));
        Assert.False(File.Exists(Path.Combine(root, "data", "migrations", "PU__create_database.sql")));
    }

    [Fact]
    public async Task Handle_Creates_Missing_ProvisionUndo_When_Provision_Already_Exists()
    {
        var root = CreateProjectRoot();
        var migrationsPath = Path.Combine(root, "data", "migrations");
        await File.WriteAllTextAsync(Path.Combine(migrationsPath, "P__bootstrap.sql"), "-- existing provision");
        var handler = new CreateMigrationScriptCommandHandler(new StubMigrationVersionProvider());

        var result = await handler.Handle(new CreateMigrationScriptCommand
        {
            HasUndo = true,
            IsProvision = true,
            Name = "bootstrap",
            Root = root
        }, CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.True(File.Exists(Path.Combine(migrationsPath, "P__bootstrap.sql")));
        Assert.True(File.Exists(Path.Combine(migrationsPath, "PU__bootstrap.sql")));
    }

    [Fact]
    public async Task Handle_Does_Not_Create_Provision_When_ProvisionUndo_Already_Exists_And_Undo_Is_Requested()
    {
        var root = CreateProjectRoot();
        var migrationsPath = Path.Combine(root, "data", "migrations");
        await File.WriteAllTextAsync(Path.Combine(migrationsPath, "PU__bootstrap.sql"), "-- existing provision undo");
        var handler = new CreateMigrationScriptCommandHandler(new StubMigrationVersionProvider());

        var result = await handler.Handle(new CreateMigrationScriptCommand
        {
            HasUndo = true,
            IsProvision = true,
            Name = "bootstrap",
            Root = root
        }, CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.False(File.Exists(Path.Combine(migrationsPath, "P__bootstrap.sql")));
    }

    [Fact]
    public async Task Handle_Rejects_Provision_Repeatable_Combination()
    {
        var root = CreateProjectRoot();
        var handler = new CreateMigrationScriptCommandHandler(new StubMigrationVersionProvider());

        var result = await handler.Handle(new CreateMigrationScriptCommand
        {
            HasUndo = true,
            IsProvision = true,
            IsRepeatable = true,
            Name = "bootstrap",
            Root = root
        }, CancellationToken.None);

        Assert.False(result.IsValid);
    }

    private static string CreateProjectRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "b8agrate-tests", Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(Path.Combine(root, "data", "migrations"));

        return root;
    }

    private sealed class StubMigrationVersionProvider : IMigrationVersionProvider
    {
        public string GetNextVersion(string path, VersioningOptions options) => "000001";
    }
}