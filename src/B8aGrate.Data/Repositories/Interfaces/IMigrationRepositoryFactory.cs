using B8aGrate.Domain.ValueObjects;

namespace B8aGrate.Data.Repositories.Interfaces;

public interface IMigrationRepositoryFactory
{
    IMigrationRepository Create(ProviderType provider, string connectionString, string? adminConnectionString, string schema, string table, int commandTimeout);
}