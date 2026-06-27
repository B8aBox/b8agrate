using System.Data.Common;
using B8aGrate.Domain.Entities;
using B8aGrate.Domain.ValueObjects;

namespace B8aGrate.Data.Repositories.Interfaces;

public interface IMigrationRepository
{
    Task<IDisposable> AcquireLock(DbConnection connection, CancellationToken cancellationToken);
    Task<int> DeleteFailedRows(DbConnection connection);
    Task EnsureMigrationTable(DbConnection connection, CancellationToken cancellationToken);
    Task ExecuteScript(DbConnection connection, string sql, MigrationTransactionMode transactionMode, CancellationToken cancellationToken);
    Task ExecuteScriptAndInsertMigration(DbConnection connection, Migration migration, string sql, CancellationToken cancellationToken);
    Task<IReadOnlyList<Migration>> GetMigrationList(DbConnection connection, CancellationToken cancellationToken);
    Task InsertMigration(DbConnection connection, Migration migration, CancellationToken cancellationToken);
    Task InsertMigration(DbConnection connection, DbTransaction? transaction, Migration migration, CancellationToken cancellationToken);
    Task<DbConnection> OpenAdminConnection(CancellationToken cancellationToken);
    Task<DbConnection> OpenConnection(CancellationToken cancellationToken);
    Task<int> UpdateChecksum(DbConnection connection, long id, string checksum);
}