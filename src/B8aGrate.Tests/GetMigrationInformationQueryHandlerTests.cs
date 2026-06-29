using B8aGrate.Application.Features.GetMigrationInformation;
using B8aGrate.Data.Repositories.Interfaces;
using B8aGrate.Data.Services.Interfaces;
using B8aGrate.Domain.Entities;
using B8aGrate.Domain.ValueObjects;
using Xunit;

namespace B8aGrate.Tests;

public sealed class GetMigrationInformationQueryHandlerTests
{
    [Fact]
    public async Task Handle_MissingConnectionString_ReturnsGenericFailure()
    {
        var root = CreateTempRoot();
        var handler = new GetMigrationInformationQueryHandler(new ThrowingMigrationScriptDiscoverer(), new ThrowingMigrationRepositoryFactory());

        var result = await handler.Handle(new GetMigrationInformationQuery
        {
            Root = root
        }, CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Null(result.Content);
        Assert.Contains(result.Detail, detail => detail.Message.ToString().Contains("Missing --connection", StringComparison.Ordinal));
    }

    private static string CreateTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "b8agrate-tests", Guid.NewGuid().ToString("N"));
        var environmentVariable = $"B8AGRATE_TEST_MISSING_{Guid.NewGuid():N}";

        Directory.CreateDirectory(root);
        Environment.SetEnvironmentVariable(environmentVariable, null);

        var configuration = new Configuration
        {
            EnvironmentVariables = new EnvironmentVariableOptions
            {
                Connection = environmentVariable
            }
        };

        File.WriteAllText(Configuration.GetPath(root), configuration.ToJson());

        return root;
    }

    private sealed class ThrowingMigrationScriptDiscoverer : IMigrationScriptDiscoverer
    {
        public IReadOnlyList<Migration> Discover(string path) => throw new InvalidOperationException("Discovery should not run.");
    }

    private sealed class ThrowingMigrationRepositoryFactory : IMigrationRepositoryFactory
    {
        public IMigrationRepository Create(ProviderType provider, string connectionString, string? adminConnectionString, string schema, string table,
            int commandTimeout) => throw new InvalidOperationException("Repository creation should not run.");
    }
}