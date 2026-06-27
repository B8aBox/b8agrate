using System.Data.Common;
using System.Text.RegularExpressions;
using B8aGrate.Data.Sql.Repositories.Abstract;
using B8aGrate.Data.Sql.Repositories.Models;
using Microsoft.Data.SqlClient;

namespace B8aGrate.Data.Sql.Repositories;

public sealed partial class SqlServerMigrationRepository(
    string connectionString, string? adminConnectionString, string schema, string table, int commandTimeout) : MigrationRepositoryBase
{
    #region Private Properties

    private string ExecutionMomentDefaultConstraint { get; } = QuoteIdentifier(ValidateIdentifier($"DF_{table}_ExecutionMoment", nameof(table)));

    private string Schema { get; } = QuoteIdentifier(ValidateIdentifier(schema, nameof(schema)));

    private string SchemaName { get; } = ValidateIdentifier(schema, nameof(schema));

    private string ScriptIndex { get; } = QuoteIdentifier(ValidateIdentifier($"IX_{table}_Script", nameof(table)));

    private string Table { get; } = QuoteIdentifier(ValidateIdentifier(table, nameof(table)));

    private string TableName { get; } = ValidateIdentifier(table, nameof(table));

    private string VersionIndex { get; } = QuoteIdentifier(ValidateIdentifier($"IX_{table}_Version", nameof(table)));

    #endregion


    #region Protected Properties

    protected override string DeleteFailedRowsSql => $"DELETE FROM {Schema}.{Table} WHERE [IsSuccess] = 0";

    protected override string InsertMigrationSql => $"""
                                                     INSERT INTO {Schema}.{Table}
                                                     (
                                                         [Checksum],
                                                         [Description],
                                                         [ExecutionMilliseconds],
                                                         [ExecutionMoment],
                                                         [IsSuccess],
                                                         [Kind],
                                                         [Script],
                                                         [Version]
                                                     )
                                                     OUTPUT INSERTED.[Id]
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
                                                     );
                                                     """;

    protected override string SelectMigrationsSql =>
        $"SELECT [Id], [Checksum], [Description], [ExecutionMilliseconds], [ExecutionMoment], [IsSuccess], [Kind], [Script], [Version] FROM {Schema}.{Table} ORDER BY [Id]";

    protected override string UpdateChecksumSql => $"UPDATE {Schema}.{Table} SET [Checksum] = @Checksum WHERE [Id] = @Id";

    #endregion


    #region Public Methods

    public override async Task<IDisposable> AcquireLock(DbConnection connection, CancellationToken cancellationToken)
    {
        var lockDatabase = connection.Database;

        await using var command = CreateCommand(connection, $"""
                                                             USE {QuoteIdentifier(lockDatabase)};
                                                             DECLARE @result int;
                                                             EXEC @result = sp_getapplock @Resource = 'B8aGrate', @LockMode = 'Exclusive', @LockOwner = 'Session', @LockTimeout = 60000;
                                                             SELECT @result;
                                                             """);

        var result = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));

        if (result < 0)
            throw new TimeoutException($"Could not acquire SQL Server application lock B8aGrate. sp_getapplock returned {result}.");

        return new AsyncLock(async () =>
        {
            await using var release = CreateCommand(connection, $"""
                                                                 USE {QuoteIdentifier(lockDatabase)};
                                                                 DECLARE @result int;
                                                                 EXEC @result = sp_releaseapplock @Resource = 'B8aGrate', @LockOwner = 'Session';
                                                                 SELECT @result;
                                                                 """);

            await release.ExecuteScalarAsync(cancellationToken);
        });
    }

    public override async Task EnsureMigrationTable(DbConnection connection, CancellationToken cancellationToken)
    {
        var sql = $"""
                   IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = @SchemaName)
                       EXEC(N'CREATE SCHEMA {Schema}');

                   IF OBJECT_ID(@QualifiedTableName, N'U') IS NULL
                   BEGIN
                       CREATE TABLE {Schema}.{Table}
                       (
                           [Id] BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                           [Checksum] NVARCHAR(128) NULL,
                           [Description] NVARCHAR(512) NOT NULL,
                           [ExecutionMilliseconds] BIGINT NOT NULL,
                           [ExecutionMoment] DATETIMEOFFSET NOT NULL CONSTRAINT {ExecutionMomentDefaultConstraint} DEFAULT SYSDATETIMEOFFSET(),
                           [IsSuccess] BIT NOT NULL,
                           [Kind] NVARCHAR(32) NOT NULL,
                           [Script] NVARCHAR(512) NOT NULL,
                           [Version] NVARCHAR(128) NULL
                       );
                       
                       CREATE INDEX {ScriptIndex} ON {Schema}.{Table} ([Script]);
                       CREATE INDEX {VersionIndex} ON {Schema}.{Table} ([Version]);
                   END
                   """;

        await using var command = CreateCommand(connection, sql);

        Add(command, "SchemaName", SchemaName);
        Add(command, "QualifiedTableName", $"{SchemaName}.{TableName}");

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public override Task<DbConnection> OpenAdminConnection(CancellationToken cancellationToken) =>
        OpenConnection(string.IsNullOrWhiteSpace(adminConnectionString) ? connectionString : adminConnectionString, cancellationToken);

    public override Task<DbConnection> OpenConnection(CancellationToken cancellationToken) => OpenConnection(connectionString, cancellationToken);

    #endregion


    #region Protected Methods

    protected override DbCommand CreateCommand(DbConnection connection, string sql)
    {
        var command = new SqlCommand(sql, (SqlConnection)connection);

        command.CommandTimeout = commandTimeout;

        return command;
    }

    protected override IReadOnlyList<string> SplitStatements(string sql) => StatementRegex().Split(sql).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();

    #endregion


    #region Private Methods

    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_]*$")]
    private static partial Regex IdentifierRegex();

    private static async Task<DbConnection> OpenConnection(string connectionString, CancellationToken cancellationToken)
    {
        var connection = new SqlConnection(connectionString);

        try
        {
            await connection.OpenAsync(cancellationToken);
        }
        catch
        {
            SqlConnection.ClearPool(connection);

            await connection.DisposeAsync();

            throw;
        }

        return connection;
    }

    private static string QuoteIdentifier(string value) => $"[{value.Replace("]", "]]", StringComparison.Ordinal)}]";

    [GeneratedRegex(@"^\s*GO\s*;?\s*$", RegexOptions.IgnoreCase | RegexOptions.Multiline, "en-US")]
    private static partial Regex StatementRegex();

    private static string ValidateIdentifier(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("SQL Server identifiers cannot be empty.", parameterName);

        if (value.Length > 128)
            throw new ArgumentException("SQL Server identifiers cannot exceed 128 characters.", parameterName);

        if (!IdentifierRegex().IsMatch(value))
            throw new ArgumentException("SQL Server identifiers may only contain letters, numbers, and underscores, and must not start with a number.",
                parameterName);

        return value;
    }

    #endregion
}