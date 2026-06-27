using System.Data.Common;
using System.Text.RegularExpressions;
using B8aGrate.Data.Sql.Repositories.Abstract;
using B8aGrate.Data.Sql.Repositories.Models;
using Npgsql;

namespace B8aGrate.Data.Sql.Repositories;

public sealed partial class PostgresMigrationRepository(string connectionString, string? adminConnectionString, string schema, string table, int commandTimeout)
    : MigrationRepositoryBase
{
    #region Private Properties

    private string Schema { get; } = QuoteIdentifier(ValidateIdentifier(schema, nameof(schema)));

    private string ScriptIndex { get; } = QuoteIdentifier(ValidateIdentifier($"IX_{table}_Script", nameof(table)));

    private string Table { get; } = QuoteIdentifier(ValidateIdentifier(table, nameof(table)));

    private string VersionIndex { get; } = QuoteIdentifier(ValidateIdentifier($"IX_{table}_Version", nameof(table)));

    #endregion


    #region Protected Properties

    protected override string DeleteFailedRowsSql => $"""
                                                      DELETE FROM {Schema}.{Table} WHERE "IsSuccess" = false
                                                      """;

    protected override string InsertMigrationSql => $"""
                                                     INSERT INTO {Schema}.{Table}
                                                     (
                                                         "Checksum",
                                                         "Description",
                                                         "ExecutionMilliseconds",
                                                         "ExecutionMoment",
                                                         "IsSuccess",
                                                         "Kind",
                                                         "Script",
                                                         "Version"
                                                     )
                                                     VALUES
                                                     (
                                                         @Checksum,
                                                         @Description,
                                                         @ExecutionMilliseconds,
                                                         @ExecutionMoment,
                                                         @IsSuccess,
                                                         @Kind,
                                                         @Script,
                                                         @Version
                                                     )
                                                     RETURNING "Id";
                                                     """;

    protected override string SelectMigrationsSql => $"""
                                                      SELECT "Id", "Checksum", "Description", "ExecutionMilliseconds", "ExecutionMoment", "IsSuccess", "Kind", "Script", "Version" FROM {Schema}.{Table} ORDER BY "Id"
                                                      """;

    protected override string UpdateChecksumSql => $"""
                                                    UPDATE {Schema}.{Table} SET "Checksum" = @Checksum WHERE "Id" = @Id
                                                    """;

    #endregion


    #region Public Methods

    public override async Task<IDisposable> AcquireLock(DbConnection connection, CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(connection, "SELECT pg_advisory_lock(hashtext('B8aGrate'));");

        await command.ExecuteNonQueryAsync(cancellationToken);

        return new AsyncLock(async () =>
        {
            await using var release = CreateCommand(connection, "SELECT pg_advisory_unlock(hashtext('B8aGrate'));");

            await release.ExecuteNonQueryAsync(cancellationToken);
        });
    }

    public override async Task EnsureMigrationTable(DbConnection connection, CancellationToken cancellationToken)
    {
        var sql = $"""
                   CREATE SCHEMA IF NOT EXISTS {Schema};

                   CREATE TABLE IF NOT EXISTS {Schema}.{Table}
                   (
                       "Id" BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                       "Checksum" VARCHAR(128) NULL,
                       "Description" VARCHAR(512) NOT NULL,
                       "ExecutionMilliseconds" BIGINT NOT NULL,
                       "ExecutionMoment" TIMESTAMPTZ NOT NULL DEFAULT now(),
                       "IsSuccess" BOOLEAN NOT NULL,
                       "Kind" VARCHAR(32) NOT NULL,
                       "Script" VARCHAR(512) NOT NULL,
                       "Version" VARCHAR(128) NULL
                   );

                   CREATE INDEX IF NOT EXISTS {VersionIndex} ON {Schema}.{Table} ("Version");
                   CREATE INDEX IF NOT EXISTS {ScriptIndex} ON {Schema}.{Table} ("Script");
                   """;

        await using var command = CreateCommand(connection, sql);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public override Task<DbConnection> OpenAdminConnection(CancellationToken cancellationToken) =>
        OpenConnection(string.IsNullOrWhiteSpace(adminConnectionString) ? connectionString : adminConnectionString, cancellationToken);

    public override Task<DbConnection> OpenConnection(CancellationToken cancellationToken) => OpenConnection(connectionString, cancellationToken);

    #endregion


    #region Protected Methods

    protected override DbCommand CreateCommand(DbConnection connection, string sql)
    {
        var command = new NpgsqlCommand(sql, (NpgsqlConnection)connection);

        command.CommandTimeout = commandTimeout;

        return command;
    }

    protected override IReadOnlyList<string> SplitStatements(string sql) => new[] { sql }.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();

    #endregion


    #region Private Methods

    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_]*$")]
    private static partial Regex IdentifierRegex();

    private static async Task<DbConnection> OpenConnection(string connectionString, CancellationToken cancellationToken)
    {
        var connection = new NpgsqlConnection(connectionString);

        await connection.OpenAsync(cancellationToken);

        return connection;
    }

    private static string QuoteIdentifier(string value) => $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";

    private static string ValidateIdentifier(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("PostgreSQL identifiers cannot be empty.", parameterName);

        if (value.Length > 63)
            throw new ArgumentException("PostgreSQL identifiers cannot exceed 63 characters.", parameterName);

        if (!IdentifierRegex().IsMatch(value))
            throw new ArgumentException("PostgreSQL identifiers may only contain letters, numbers, and underscores, and must not start with a number.",
                parameterName);

        return value;
    }

    #endregion
}