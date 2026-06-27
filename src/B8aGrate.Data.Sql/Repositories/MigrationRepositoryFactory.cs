using B8aGrate.Data.Repositories.Interfaces;
using B8aGrate.Domain.ValueObjects;

namespace B8aGrate.Data.Sql.Repositories;

public sealed class MigrationRepositoryFactory : IMigrationRepositoryFactory
{
    public IMigrationRepository Create(ProviderType provider, string connectionString, string? adminConnectionString, string schema, string table,
        int commandTimeout) => provider switch
    {
        ProviderType.PostgreSql => new PostgresMigrationRepository(connectionString, adminConnectionString, schema, table, commandTimeout),
        ProviderType.SqlServer => new SqlServerMigrationRepository(connectionString, adminConnectionString, schema, table, commandTimeout),
        _ => throw new InvalidOperationException("Provider must be sqlserver or postgres.")
    };
}