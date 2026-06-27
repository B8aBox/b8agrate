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

namespace B8aGrate.Application.Features.UndoProvisioning;

public sealed class UndoProvisioningCommandHandler(IMigrationScriptDiscoverer migrationScriptDiscoverer, IMigrationRepositoryFactory migrationRepositoryFactory)
    : MigrationHandlerBase<UndoProvisioningCommand, Result<UndoProvisioningProjection>>(migrationRepositoryFactory),
        IRequestHandler<UndoProvisioningCommand, Result<UndoProvisioningProjection>>
{
    public Task<Result<UndoProvisioningProjection>> Handle(UndoProvisioningCommand request, CancellationToken cancellationToken) => Handle(request,
        async (configuration, migrationRepository) =>
        {
            await using var adminConnection = await migrationRepository.OpenAdminConnection(cancellationToken);
            using var adminLock = await migrationRepository.AcquireLock(adminConnection, cancellationToken);

            IReadOnlyList<Migration> migrations;

            await using (var connection = await OpenConnection(migrationRepository, cancellationToken))
                migrations = await migrationRepository.GetMigrationList(connection, cancellationToken);

            if (!migrations.IsProvisionApplied())
                return Results.Failure<UndoProvisioningProjection>("No applied provisioning record was found.");

            var migrationsPath = Path.GetFullPath(Path.Combine(request.Root, configuration.Migration.Path));
            var migrationScripts = migrationScriptDiscoverer.Discover(migrationsPath);
            var discoveryErrors = migrationScripts.GetDiscoveryErrors();

            if (discoveryErrors.Count != 0)
                return Results.Failure<UndoProvisioningProjection>(discoveryErrors);

            var pendingUndoProvisionScript = migrationScripts.SingleOrDefault(x => x.Kind == MigrationKind.ProvisionUndo);

            if (pendingUndoProvisionScript == null)
                return Results.Failure<UndoProvisioningProjection>("No provision undo script found.", "MissingProvisionUndo");

            var appliedUndoProvisionMigrations = new List<Migration>();

            var projection = new UndoProvisioningProjection
            {
                AppliedMigrations = appliedUndoProvisionMigrations,
                Message = "Provision undo scripts are recorded as ProvisionUndo rows. The original Provision rows are preserved for audit history."
            };

            if (request.IsDryRun)
            {
                appliedUndoProvisionMigrations.Add(pendingUndoProvisionScript);

                return Results.Success(projection);
            }

            appliedUndoProvisionMigrations.Add(await RunMigrationScript(migrationRepository, adminConnection, null, pendingUndoProvisionScript,
                migrationsPath, request.IsDryRun, cancellationToken));

            DbConnection? migrationConnection;

            try
            {
                migrationConnection = await OpenConnection(migrationRepository, cancellationToken);
            }
            catch
            {
                projection.Message =
                    "Provision undo completed, but the target connection could not be opened afterward, so ProvisionUndo history rows were not recorded.";

                return Results.Success(projection);
            }

            await using (migrationConnection)
            {
                foreach (var migration in appliedUndoProvisionMigrations)
                    await migrationRepository.InsertMigration(migrationConnection, migration, cancellationToken);
            }

            return Results.Success(projection);
        }, cancellationToken);
}
