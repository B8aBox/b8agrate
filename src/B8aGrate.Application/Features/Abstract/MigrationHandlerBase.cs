using System.Data.Common;
using System.Diagnostics;
using B8aGrate.Application.Extensions;
using B8aGrate.Data.Repositories.Interfaces;
using B8aGrate.Domain.Entities;
using B8aGrate.Domain.Validation;
using B8aGrate.Domain.ValueObjects;
using YuckQi.Domain.Validation;

namespace B8aGrate.Application.Features.Abstract;

public abstract class MigrationHandlerBase<TRequest, TResult>(IMigrationRepositoryFactory migrationRepositoryFactory) where TRequest : MigrationRequestBase
    where TResult : Result
{
    #region Protected Methods

    protected async Task<TResult> Handle(TRequest request, Func<Configuration, IMigrationRepository, Task<TResult>> migrationHandler,
        CancellationToken cancellationToken)
    {
        var configuration = await Configuration.Load(request.Root, cancellationToken);
        var connectionStringResult = GetConnectionString(request.ConnectionString, configuration.EnvironmentVariables.Connection);

        if (!connectionStringResult.IsValid)
            return FailureResult(connectionStringResult.Detail);

        var connectionString = connectionStringResult.Content ?? throw new InvalidOperationException("Connection string cannot be retrieved.");
        var migrationRepository = GetMigrationRepository(request, configuration, connectionString);

        return await migrationHandler(configuration, migrationRepository);
    }

    protected Task<TResult> Handle(TRequest request, Func<Configuration, DbConnection, IMigrationRepository, Task<TResult>> migrationHandler,
        CancellationToken cancellationToken) => Handle(request, async (configuration, migrationRepository) =>
    {
        await using var connection = await OpenConnection(migrationRepository, cancellationToken);

        return await migrationHandler(configuration, connection, migrationRepository);
    }, cancellationToken);

    protected Result<string> GetConnectionString(string? connectionString, string environmentVariable)
    {
        connectionString ??= Environment.GetEnvironmentVariable(environmentVariable);

        return string.IsNullOrWhiteSpace(connectionString)
            ? Results.Failure<string>($"Missing --connection and environment variable {environmentVariable} is not set.")
            : Results.Success(connectionString);
    }

    protected IMigrationRepository GetMigrationRepository(Configuration configuration, string connectionString, string? adminConnectionString = null) =>
        migrationRepositoryFactory.Create(configuration.Provider, connectionString, adminConnectionString, configuration.Migration.Schema,
            configuration.Migration.Table, configuration.Command.Timeout);

    protected async Task<DbConnection> OpenConnection(IMigrationRepository migrationRepository, CancellationToken cancellationToken)
    {
        var connection = await migrationRepository.OpenConnection(cancellationToken);

        try
        {
            await migrationRepository.EnsureMigrationTable(connection, cancellationToken);

            return connection;
        }
        catch
        {
            await connection.DisposeAsync();

            throw;
        }
    }

    protected Task<Migration> RunMigrationScript(IMigrationRepository migrationRepository, DbConnection connection, Migration migrationScript,
        string migrationsPath, bool isDryRun, CancellationToken cancellationToken) => RunMigrationScript(migrationRepository, connection, connection,
        migrationScript, migrationsPath, isDryRun, cancellationToken);

    protected async Task<Migration> RunMigrationScript(IMigrationRepository migrationRepository, DbConnection executionConnection,
        DbConnection? migrationConnection, Migration migrationScript, string migrationsPath, bool isDryRun, CancellationToken cancellationToken)
    {
        if (isDryRun)
            return migrationScript;

        if (string.IsNullOrWhiteSpace(migrationScript.Checksum) || string.IsNullOrWhiteSpace(migrationScript.Sql))
        {
            var path = Path.Combine(migrationsPath, migrationScript.Script);
            var sql = await File.ReadAllTextAsync(path, cancellationToken);

            migrationScript.Checksum = sql.GetChecksum();
            migrationScript.Sql = sql;
        }

        migrationScript.ExecutionMoment = DateTimeOffset.UtcNow;

        var stopwatch = Stopwatch.StartNew();

        var canRecordAtomically = migrationConnection != null &&
                                  ReferenceEquals(executionConnection, migrationConnection) &&
                                  !HasDirective(migrationScript.Sql, "transaction", "false");

        try
        {
            migrationScript.IsSuccess = true;

            if (canRecordAtomically)
            {
                await migrationRepository.ExecuteScriptAndInsertMigration(executionConnection, migrationScript, migrationScript.Sql, cancellationToken);
            }
            else
            {
                var transactionMode = migrationScript.Kind is MigrationKind.Provision or MigrationKind.ProvisionUndo
                    ? MigrationTransactionMode.None
                    : MigrationTransactionMode.Required;

                await migrationRepository.ExecuteScript(executionConnection, migrationScript.Sql, transactionMode, cancellationToken);

                stopwatch.Stop();

                migrationScript.ExecutionMilliseconds = stopwatch.ElapsedMilliseconds;

                if (migrationConnection != null)
                    await migrationRepository.InsertMigration(migrationConnection, migrationScript, cancellationToken);
            }

            return migrationScript;
        }
        catch
        {
            stopwatch.Stop();

            if (migrationConnection != null)
            {
                migrationScript.ExecutionMilliseconds = stopwatch.ElapsedMilliseconds;
                migrationScript.IsSuccess = false;

                await migrationRepository.InsertMigration(migrationConnection, migrationScript, cancellationToken);
            }

            throw;
        }
    }

    protected async Task<DbConnection?> TryOpenConnection(IMigrationRepository migrationRepository, CancellationToken cancellationToken)
    {
        try
        {
            return await OpenConnection(migrationRepository, cancellationToken);
        }
        catch
        {
            return null;
        }
    }

    #endregion


    #region Private Methods

    private static TResult FailureResult(IReadOnlyCollection<ResultDetail> details)
    {
        if (typeof(TResult) == typeof(Result))
            return (TResult)Results.Failure(details);

        if (!typeof(TResult).IsGenericType || typeof(TResult).GetGenericTypeDefinition() != typeof(Result<>))
            throw new NotSupportedException($"Unsupported migration handler result type {typeof(TResult)}.");

        var contentType = typeof(TResult).GetGenericArguments()[0];

        var failureMethod = typeof(Results).GetMethods().Single(method =>
            method is { Name: nameof(Results.Failure), IsGenericMethodDefinition: true } &&
            method.GetParameters().Length == 1 &&
            method.GetParameters()[0].ParameterType == typeof(IReadOnlyCollection<ResultDetail>));

        return (TResult)failureMethod.MakeGenericMethod(contentType).Invoke(null, [details])!;
    }

    private IMigrationRepository GetMigrationRepository(TRequest request, Configuration configuration, string connectionString)
    {
        if (request is not AdminMigrationRequestBase adminRequest)
            return GetMigrationRepository(configuration, connectionString);

        var adminConnectionString =
            adminRequest.AdminConnectionString ?? Environment.GetEnvironmentVariable(configuration.EnvironmentVariables.AdminConnection);

        return GetMigrationRepository(configuration, connectionString, adminConnectionString);
    }

    private static bool HasDirective(string sql, string key, string value) =>
        (from line in sql.Split('\n').Take(20)
         select line.Trim()
         into trimmed
         where trimmed.StartsWith("-- migrate:", StringComparison.OrdinalIgnoreCase)
         select trimmed[11..].Trim()).Any(directive => directive.Equals($"{key}={value}", StringComparison.OrdinalIgnoreCase));

    #endregion
}