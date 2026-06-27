using B8aGrate.Application.Extensions;
using B8aGrate.Application.Features.Abstract;
using B8aGrate.Data.Repositories.Interfaces;
using B8aGrate.Data.Services.Interfaces;
using B8aGrate.Domain.Projections;
using B8aGrate.Domain.Validation;
using MediatR;
using YuckQi.Domain.Validation;

namespace B8aGrate.Application.Features.GetMigrationInformation;

public sealed class GetMigrationInformationQueryHandler(
    IMigrationScriptDiscoverer migrationScriptDiscoverer, IMigrationRepositoryFactory migrationRepositoryFactory)
    : MigrationHandlerBase<GetMigrationInformationQuery, Result<MigrationInformationProjection>>(migrationRepositoryFactory),
        IRequestHandler<GetMigrationInformationQuery, Result<MigrationInformationProjection>>
{
    public Task<Result<MigrationInformationProjection>> Handle(GetMigrationInformationQuery request, CancellationToken cancellationToken) => Handle(request,
        async (configuration, connection, migrationRepository) =>
        {
            var migrations = await migrationRepository.GetMigrationList(connection, cancellationToken);
            var appliedVersions = migrations.GetAppliedVersions();
            var baselineVersion = migrations.GetBaselineVersion();
            var migrationsPath = Path.GetFullPath(Path.Combine(request.Root, configuration.Migration.Path));
            var migrationScripts = migrationScriptDiscoverer.Discover(migrationsPath);
            var errors = migrationScripts.GetDiscoveryErrors();
            var pendingProvisionScripts = migrationScripts.GetPendingProvisionScripts(migrations.IsProvisionApplied());

            if (pendingProvisionScripts.Count > 0)
                errors.Add(new ResultDetail(
                    $"Provision script {pendingProvisionScripts[0].Script} is pending, but provision scripts are bootstrap-only and the target database is already reachable.",
                    "PendingProvisionScript"));

            var projection = new MigrationInformationProjection
            {
                AppliedMigrations = migrations.GetSuccessfulMigrations(),
                BaselineVersion = baselineVersion,
                Errors = errors,
                PendingMigrations = migrationScripts.GetPendingVersionedMigrationScripts(appliedVersions, baselineVersion)
                                                    .OrderBy(x => x.Version, StringComparer.OrdinalIgnoreCase).ToList(),
                Provider = configuration.Provider
            };

            return Results.Success(projection);
        }, cancellationToken);
}