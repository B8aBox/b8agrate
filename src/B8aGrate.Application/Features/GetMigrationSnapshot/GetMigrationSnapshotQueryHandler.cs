using B8aGrate.Application.Extensions;
using B8aGrate.Application.Features.Abstract;
using B8aGrate.Data.Repositories.Interfaces;
using B8aGrate.Data.Services.Interfaces;
using B8aGrate.Domain.Projections;
using B8aGrate.Domain.Validation;
using MediatR;
using YuckQi.Domain.Validation;

namespace B8aGrate.Application.Features.GetMigrationSnapshot;

public sealed class GetMigrationSnapshotQueryHandler(
    IMigrationScriptDiscoverer migrationScriptDiscoverer, IMigrationRepositoryFactory migrationRepositoryFactory)
    : MigrationHandlerBase<GetMigrationSnapshotQuery, Result<MigrationSnapshotProjection>>(migrationRepositoryFactory),
        IRequestHandler<GetMigrationSnapshotQuery, Result<MigrationSnapshotProjection>>
{
    public Task<Result<MigrationSnapshotProjection>> Handle(GetMigrationSnapshotQuery request, CancellationToken cancellationToken) => Handle(request,
        async (configuration, connection, migrationRepository) =>
        {
            var migrations = await migrationRepository.GetMigrationList(connection, cancellationToken);
            var appliedVersions = migrations.GetAppliedVersions();
            var baselineVersion = migrations.GetBaselineVersion();
            var currentVersion = appliedVersions.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).LastOrDefault() ?? baselineVersion ?? "<none>";
            var migrationsPath = Path.GetFullPath(Path.Combine(request.Root, configuration.Migration.Path));
            var migrationScripts = migrationScriptDiscoverer.Discover(migrationsPath);
            var pendingProvisionScripts = migrationScripts.GetPendingProvisionScripts(migrations.IsProvisionApplied());
            var errors = migrations.GetFailedMigrationErrors();

            if (pendingProvisionScripts.Count > 0)
                errors.Add(new ResultDetail(
                    $"Provision script {pendingProvisionScripts[0].Script} is pending, but provision scripts are bootstrap-only and the target database is already reachable.",
                    "PendingProvisionScript"));

            var pendingVersionedMigrationScripts = migrationScripts.GetPendingVersionedMigrationScripts(appliedVersions, baselineVersion);
            var enrichedMigrationScripts = await migrationScripts.EnrichMigrationScripts(migrationsPath, cancellationToken);
            var repeatableMigrations = migrations.GetLatestSuccessfulRepeatableMigrations();
            var pendingRepeatableMigrationScripts = enrichedMigrationScripts.GetPendingRepeatableMigrationScripts(repeatableMigrations);

            errors.AddRange(migrations.Validate(enrichedMigrationScripts.ToDictionary(x => x.Script, StringComparer.OrdinalIgnoreCase)));
            errors.AddRange(migrationScripts.GetDiscoveryErrors());

            var projection = new MigrationSnapshotProjection
            {
                AppliedMigrations = migrations.GetSuccessfulMigrations(),
                CurrentVersion = currentVersion,
                Errors = errors.OrderBy(x => $"{x.Code}", StringComparer.OrdinalIgnoreCase).ToList(),
                PendingMigrations = pendingVersionedMigrationScripts
                                    .Concat(pendingRepeatableMigrationScripts)
                                    .OrderBy(x => x.Kind)
                                    .ThenBy(x => x.Version, StringComparer.OrdinalIgnoreCase)
                                    .ThenBy(x => x.Script, StringComparer.OrdinalIgnoreCase)
                                    .ToList(),
                Provider = configuration.Provider
            };

            return Results.Success(projection);
        }, cancellationToken);
}