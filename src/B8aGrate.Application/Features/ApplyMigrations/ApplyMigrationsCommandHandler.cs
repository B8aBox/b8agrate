using System.Data.Common;
using B8aGrate.Application.Extensions;
using B8aGrate.Application.Features.Abstract;
using B8aGrate.Data.Repositories.Interfaces;
using B8aGrate.Data.Services.Interfaces;
using B8aGrate.Domain.Entities;
using B8aGrate.Domain.Projections;
using B8aGrate.Domain.Validation;
using B8aGrate.Domain.ValueObjects;
using MediatR;
using YuckQi.Domain.Validation;

namespace B8aGrate.Application.Features.ApplyMigrations;

public sealed class ApplyMigrationsCommandHandler(IMigrationScriptDiscoverer migrationScriptDiscoverer, IMigrationRepositoryFactory migrationRepositoryFactory)
    : MigrationHandlerBase<ApplyMigrationsCommand, Result<ApplyMigrationsProjection>>(migrationRepositoryFactory),
        IRequestHandler<ApplyMigrationsCommand, Result<ApplyMigrationsProjection>>
{
    public Task<Result<ApplyMigrationsProjection>> Handle(ApplyMigrationsCommand request, CancellationToken cancellationToken) => Handle(request,
        async (configuration, migrationRepository) =>
        {
            var migrationsPath = Path.GetFullPath(Path.Combine(request.Root, configuration.Migration.Path));
            var migrationScripts = migrationScriptDiscoverer.Discover(migrationsPath);
            var discoveryErrors = migrationScripts.GetDiscoveryErrors();

            if (discoveryErrors.Count > 0)
                return Results.Failure<ApplyMigrationsProjection>(discoveryErrors);

            var provisionScripts = migrationScripts
                                   .Where(x => x.Kind == MigrationKind.Provision)
                                   .OrderBy(x => x.Version, StringComparer.OrdinalIgnoreCase)
                                   .ToList();

            if (provisionScripts.Count > 1)
                return Results.Failure<ApplyMigrationsProjection>("Only one provision script is allowed.");

            if (provisionScripts.Count > 0)
            {
                await using var adminConnection = await migrationRepository.OpenAdminConnection(cancellationToken);
                using var adminLock = await migrationRepository.AcquireLock(adminConnection, cancellationToken);

                return await HandleMigrations(request, migrationRepository, migrationScripts, provisionScripts, migrationsPath, adminConnection,
                    cancellationToken);
            }

            return await HandleMigrations(request, migrationRepository, migrationScripts, provisionScripts, migrationsPath, null, cancellationToken);
        }, cancellationToken);

    private async Task<Result<ApplyMigrationsProjection>> HandleMigrations(ApplyMigrationsCommand request, IMigrationRepository migrationRepository,
        IReadOnlyList<Migration> migrationScripts, List<Migration> provisionScripts, string migrationsPath, DbConnection? adminConnection,
        CancellationToken cancellationToken)
    {
        var connection = provisionScripts.Count == 0
            ? await OpenConnection(migrationRepository, cancellationToken)
            : await TryOpenConnection(migrationRepository, cancellationToken);

        var appliedMigrations = new List<Migration>();
        Migration? provisionMigration = null;

        // This means we have a provision script
        if (connection == null)
        {
            if (request.IsDryRun)
                return Results.Success(new ApplyMigrationsProjection
                {
                    AppliedMigrations = provisionScripts,
                    Message = "Target connection could not be opened. Dry run preview stopped after provisioning scripts."
                });

            if (adminConnection is null)
                throw new InvalidOperationException("Admin connection cannot be retrieved.");

            provisionMigration = await RunProvisionMigration(migrationRepository, adminConnection, provisionScripts[0], migrationsPath, request.IsDryRun,
                cancellationToken);

            appliedMigrations.Add(provisionMigration);

            connection = await OpenConnectionAfterProvision(migrationRepository, provisionMigration, migrationsPath, cancellationToken);
        }

        if (connection is null)
            throw new InvalidOperationException("Target connection cannot be retrieved.");

        await using (connection)
        {
            using var migrationLock = await migrationRepository.AcquireLock(connection, cancellationToken);

            if (provisionMigration != null)
                await migrationRepository.InsertMigration(connection, provisionMigration, cancellationToken);

            var migrations = await migrationRepository.GetMigrationList(connection, cancellationToken);
            var pendingProvisionScripts = provisionScripts.GetPendingProvisionScripts(migrations.IsProvisionApplied());
            var pendingProvisionErrorResult = GetPendingProvisionScriptErrors(pendingProvisionScripts);

            if (!pendingProvisionErrorResult.IsValid)
                return Results.Failure<ApplyMigrationsProjection>(pendingProvisionErrorResult.Detail);

            var appliedVersions = migrations.GetAppliedVersions();
            var baselineVersion = migrations.GetBaselineVersion();

            var pendingVersionedScripts = migrationScripts.GetPendingVersionedMigrationScripts(appliedVersions, baselineVersion)
                                                          .OrderBy(x => x.Version, StringComparer.OrdinalIgnoreCase)
                                                          .ToList();

            var repeatableMigrations = migrations.GetLatestSuccessfulRepeatableMigrations();

            var pendingRepeatableMigrationScripts =
                await migrationScripts.GetPendingRepeatableMigrationScripts(repeatableMigrations, migrationsPath, cancellationToken);

            foreach (var migrationScript in pendingVersionedScripts.Concat(pendingRepeatableMigrationScripts))
                appliedMigrations.Add(await RunMigrationScript(migrationRepository, connection, migrationScript, migrationsPath, request.IsDryRun,
                    cancellationToken));

            var skippedMigrations = migrationScripts
                                    .Where(x =>
                                        x.Kind == MigrationKind.Versioned && baselineVersion != null &&
                                        string.Compare(x.Version, baselineVersion, StringComparison.OrdinalIgnoreCase) <= 0)
                                    .OrderBy(x => x.Version, StringComparer.OrdinalIgnoreCase)
                                    .ToList();

            return Results.Success(new ApplyMigrationsProjection
            {
                AppliedMigrations = appliedMigrations,
                SkippedMigrations = skippedMigrations
            });
        }
    }

    private static Result GetPendingProvisionScriptErrors(List<Migration> pendingProvisionScripts) => pendingProvisionScripts.Count == 0
        ? Results.Success()
        : pendingProvisionScripts.Count > 1
            ? Results.Failure("Only one provision script is allowed.")
            : Results.Failure(
                $"Provision script {pendingProvisionScripts[0].Script} is pending, but a provision script is bootstrap-only and the target database is already reachable.",
                "PendingProvisionTargetExists");

    private async Task<Migration> RunProvisionMigration(IMigrationRepository migrationRepository, DbConnection adminConnection, Migration migration,
        string migrationsPath, bool isDryRun, CancellationToken cancellationToken)
    {
        if (isDryRun)
            return migration;

        await RunMigrationScript(migrationRepository, adminConnection, null, migration, migrationsPath, isDryRun, cancellationToken);

        return migration;
    }

    private async Task<DbConnection> OpenConnectionAfterProvision(IMigrationRepository migrationRepository, Migration migration, string migrationsPath,
        CancellationToken cancellationToken)
    {
        try
        {
            return await OpenConnection(migrationRepository, cancellationToken);
        }
        catch (Exception exception)
        {
            var ran = $"Provision script ({migration.Script}) was ran {(migration.IsSuccess ? "successfully" : "unsuccessfully")}.";

            throw new InvalidOperationException(
                $"Provisioning completed, but the target connection still could not be opened. {ran} Migrations path: {migrationsPath}. Original error: {exception.Message}",
                exception);
        }
    }
}