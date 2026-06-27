using B8aGrate.Application.Features.InitializeProject;
using B8aGrate.Data.Services;
using B8aGrate.Domain.ValueObjects;
using Xunit;

namespace B8aGrate.Tests;

public sealed class InitializeProjectCommandHandlerTests
{
    [Fact]
    public async Task Handle_Writes_PostgreSql_Templates_From_Application_Assembly()
    {
        var root = CreateTempRoot();
        var handler = new InitializeProjectCommandHandler(new MigrationVersionProvider());

        var result = await handler.Handle(new InitializeProjectCommand
        {
            Path = "migrations",
            Provider = ProviderType.PostgreSql,
            Root = root,
            WithTemplates = true
        }, CancellationToken.None);

        Assert.True(result.IsValid);

        var migrationsPath = Path.Combine(root, "migrations");

        Assert.Contains("CREATE TABLE public.country", File.ReadAllText(Path.Combine(migrationsPath, "V000001__initial_schema.sql")));
        Assert.Contains("DROP TABLE IF EXISTS public.country", File.ReadAllText(Path.Combine(migrationsPath, "U000001__drop_initial_schema.sql")));
        Assert.Contains("ON CONFLICT (code) DO NOTHING", File.ReadAllText(Path.Combine(migrationsPath, "R__seed_countries.sql")));
    }

    private static string CreateTempRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), "b8agrate-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);

        return path;
    }
}