using System.Data.Common;
using System.Diagnostics;
using B8aGrate.Data.Repositories.Interfaces;
using B8aGrate.Domain.Entities;
using B8aGrate.Domain.ValueObjects;

namespace B8aGrate.Data.Sql.Repositories.Abstract;

public abstract class MigrationRepositoryBase : IMigrationRepository
{
    #region Protected Properties

    protected abstract string DeleteFailedRowsSql { get; }

    protected abstract string InsertMigrationSql { get; }

    protected abstract string SelectMigrationsSql { get; }

    protected abstract string UpdateChecksumSql { get; }

    #endregion


    #region Public Methods

    public abstract Task<IDisposable> AcquireLock(DbConnection connection, CancellationToken cancellationToken);

    public async Task<int> DeleteFailedRows(DbConnection connection)
    {
        await using var command = CreateCommand(connection, DeleteFailedRowsSql);

        return await command.ExecuteNonQueryAsync();
    }

    public abstract Task EnsureMigrationTable(DbConnection connection, CancellationToken cancellationToken);

    public async Task ExecuteScript(DbConnection connection, string sql, MigrationTransactionMode transactionMode, CancellationToken cancellationToken)
    {
        if (ShouldUseTransaction(sql, transactionMode))
        {
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

            try
            {
                foreach (var statement in SplitStatements(sql))
                {
                    await using var command = CreateCommand(connection, statement);

                    command.Transaction = transaction;

                    await command.ExecuteNonQueryAsync(cancellationToken);
                }

                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);

                throw;
            }
        }
        else
        {
            foreach (var statement in SplitStatements(sql))
            {
                await using var command = CreateCommand(connection, statement);

                await command.ExecuteNonQueryAsync(cancellationToken);
            }
        }
    }

    public async Task ExecuteScriptAndInsertMigration(DbConnection connection, Migration migration, string sql, CancellationToken cancellationToken)
    {
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            foreach (var statement in SplitStatements(sql))
            {
                await using var command = CreateCommand(connection, statement);

                command.Transaction = transaction;

                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            stopwatch.Stop();

            migration.ExecutionMilliseconds = stopwatch.ElapsedMilliseconds;

            await InsertMigration(connection, transaction, migration, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            stopwatch.Stop();

            await transaction.RollbackAsync(cancellationToken);

            throw;
        }
    }

    public async Task<IReadOnlyList<Migration>> GetMigrationList(DbConnection connection, CancellationToken cancellationToken)
    {
        var rows = new List<Migration>();

        await using var command = CreateCommand(connection, SelectMigrationsSql);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var checksum = reader.GetOrdinal("Checksum");
        var description = reader.GetOrdinal("Description");
        var executionMilliseconds = reader.GetOrdinal("ExecutionMilliseconds");
        var executionMoment = reader.GetOrdinal("ExecutionMoment");
        var id = reader.GetOrdinal("Id");
        var isSuccess = reader.GetOrdinal("IsSuccess");
        var kind = reader.GetOrdinal("Kind");
        var script = reader.GetOrdinal("Script");
        var version = reader.GetOrdinal("Version");

        while (await reader.ReadAsync(cancellationToken))
            rows.Add(new Migration
            {
                Checksum = reader.IsDBNull(checksum) ? null : reader.GetString(checksum),
                Description = reader.GetString(description),
                ExecutionMilliseconds = reader.GetInt64(executionMilliseconds),
                ExecutionMoment = reader.GetFieldValue<DateTimeOffset>(executionMoment),
                Id = reader.GetInt64(id),
                IsSuccess = reader.GetBoolean(isSuccess),
                Kind = Enum.Parse<MigrationKind>(reader.GetString(kind)),
                Script = reader.GetString(script),
                Version = reader.IsDBNull(version) ? null : reader.GetString(version)
            });

        return rows;
    }

    public Task InsertMigration(DbConnection connection, Migration migration, CancellationToken cancellationToken) =>
        InsertMigration(connection, null, migration, cancellationToken);

    public async Task InsertMigration(DbConnection connection, DbTransaction? transaction, Migration migration, CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(connection, InsertMigrationSql);

        command.Transaction = transaction;

        Add(command, nameof(Migration.Checksum), (object?)migration.Checksum ?? DBNull.Value);
        Add(command, nameof(Migration.Description), migration.Description);
        Add(command, nameof(Migration.ExecutionMilliseconds), migration.ExecutionMilliseconds);
        Add(command, nameof(Migration.ExecutionMoment), migration.ExecutionMoment);
        Add(command, nameof(Migration.IsSuccess), migration.IsSuccess);
        Add(command, nameof(Migration.Kind), migration.Kind.ToString());
        Add(command, nameof(Migration.Script), migration.Script);
        Add(command, nameof(Migration.Version), (object?)migration.Version ?? DBNull.Value);

        var id = await command.ExecuteScalarAsync(cancellationToken);

        migration.Id = Convert.ToInt64(id);
    }

    public abstract Task<DbConnection> OpenAdminConnection(CancellationToken cancellationToken);

    public abstract Task<DbConnection> OpenConnection(CancellationToken cancellationToken);

    public async Task<int> UpdateChecksum(DbConnection connection, long id, string checksum)
    {
        await using var command = CreateCommand(connection, UpdateChecksumSql);

        Add(command, "Id", id);
        Add(command, "Checksum", checksum);

        return await command.ExecuteNonQueryAsync();
    }

    #endregion


    #region Protected Methods

    protected static void Add(DbCommand cmd, string name, object value)
    {
        var parameter = cmd.CreateParameter();

        parameter.ParameterName = name;
        parameter.Value = value;

        cmd.Parameters.Add(parameter);
    }

    protected abstract DbCommand CreateCommand(DbConnection connection, string sql);

    protected abstract IReadOnlyList<string> SplitStatements(string sql);

    #endregion


    #region Private Methods

    private static bool HasDirective(string sql, string key, string value) =>
        (from line in sql.Split('\n').Take(20)
         select line.Trim()
         into trimmed
         where trimmed.StartsWith("-- migrate:", StringComparison.OrdinalIgnoreCase)
         select trimmed[11..].Trim()).Any(directive => directive.Equals($"{key}={value}", StringComparison.OrdinalIgnoreCase));

    private static bool ShouldUseTransaction(string sql, MigrationTransactionMode transactionMode) => transactionMode switch
    {
        MigrationTransactionMode.None => false,
        MigrationTransactionMode.FromDirective => HasDirective(sql, "transaction", "true"),
        MigrationTransactionMode.Required => true,
        _ => throw new ArgumentOutOfRangeException(nameof(transactionMode), transactionMode, null)
    };

    #endregion
}