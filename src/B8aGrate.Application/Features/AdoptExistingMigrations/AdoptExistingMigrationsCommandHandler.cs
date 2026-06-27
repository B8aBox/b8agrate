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

namespace B8aGrate.Application.Features.AdoptExistingMigrations;

public sealed class AdoptExistingMigrationsCommandHandler(
    IMigrationScriptDiscoverer migrationScriptDiscoverer, IMigrationRepositoryFactory migrationRepositoryFactory)
    : MigrationHandlerBase<AdoptExistingMigrationsCommand, Result<AdoptExistingMigrationsProjection>>(migrationRepositoryFactory),
        IRequestHandler<AdoptExistingMigrationsCommand, Result<AdoptExistingMigrationsProjection>>
{
    public async Task<Result<AdoptExistingMigrationsProjection>> Handle(AdoptExistingMigrationsCommand request, CancellationToken cancellationToken)
    {
        if (!request.IsAll && string.IsNullOrWhiteSpace(request.TargetVersion))
            return Results.Failure<AdoptExistingMigrationsProjection>("Missing --all or --target <version>.");

        return await Handle(request, async (configuration, connection, migrationRepository) =>
        {
            var migrationsPath = Path.GetFullPath(Path.Combine(request.Root, configuration.Migration.Path));

            var migrationScripts = migrationScriptDiscoverer.Discover(migrationsPath);
            var discoveryErrors = migrationScripts.GetDiscoveryErrors();

            var versionedMigrationScripts = migrationScripts
                                            .Where(x => x.Kind == MigrationKind.Versioned)
                                            .OrderBy(x => x.Version, StringComparer.OrdinalIgnoreCase)
                                            .ToList();

            var candidateMigrationScripts = await versionedMigrationScripts
                                                  .Where(x => request.IsAll ||
                                                              string.Compare(x.Version, request.TargetVersion, StringComparison.OrdinalIgnoreCase) <= 0)
                                                  .EnrichMigrationScripts(migrationsPath, cancellationToken);

            using var migrationLock = await migrationRepository.AcquireLock(connection, cancellationToken);
            var migrations = await migrationRepository.GetMigrationList(connection, cancellationToken);

            var existingMigrationsByVersion = migrations
                                              .Where(x => x is
                                              {
                                                  IsSuccess: true, Version: not null,
                                                  Kind: MigrationKind.Versioned or MigrationKind.Adopted or MigrationKind.Baseline
                                              })
                                              .GroupBy(x => x.Version!, StringComparer.OrdinalIgnoreCase)
                                              .ToDictionary(x => x.Key, x => x.OrderByDescending(y => y.Id).First(), StringComparer.OrdinalIgnoreCase);

            foreach (var migrationScript in candidateMigrationScripts)
            {
                if (!existingMigrationsByVersion.TryGetValue(migrationScript.Version!, out var existingMigration))
                    continue;

                if (!string.Equals(existingMigration.Script, migrationScript.Script, StringComparison.OrdinalIgnoreCase))
                    discoveryErrors.Add(new ResultDetail($"Version {migrationScript.Version} is already recorded as {existingMigration.Script}.",
                        "VersionAlreadyRecorded"));
                else if (!string.Equals(existingMigration.Checksum, migrationScript.Checksum, StringComparison.OrdinalIgnoreCase) &&
                         existingMigration.Kind != MigrationKind.Baseline)
                    discoveryErrors.Add(new ResultDetail($"Version {migrationScript.Version} is already recorded but checksum differs.", "ChecksumConflict"));
            }

            if (discoveryErrors.Count > 0 && !request.IsDryRun)
                return Results.Failure<AdoptExistingMigrationsProjection>(discoveryErrors);

            var appliedVersions = migrations.GetAppliedVersions();
            var baselineVersion = migrations.GetBaselineVersion();
            var pendingMigrationScripts = candidateMigrationScripts.Where(x => !appliedVersions.Contains(x.Version!)).Where(x => !IsCoveredByBaseline(x));
            var adoptedMigrations = new List<Migration>();

            foreach (var migrationScript in pendingMigrationScripts)
            {
                migrationScript.ExecutionMoment = DateTimeOffset.UtcNow;
                migrationScript.IsSuccess = true;
                migrationScript.Kind = MigrationKind.Adopted;

                if (!request.IsDryRun)
                    await migrationRepository.InsertMigration(connection, migrationScript, cancellationToken);

                adoptedMigrations.Add(migrationScript);
            }

            var skippedMigrations = candidateMigrationScripts.Where(x => appliedVersions.Contains(x.Version!) || IsCoveredByBaseline(x)).ToList();

            return Results.Success(new AdoptExistingMigrationsProjection
            {
                AppliedMigrations = adoptedMigrations,
                Errors = discoveryErrors,
                Message = request.IsDryRun ? "Dry run only. No migration rows were inserted." : "Adopted existing migrations.",
                SkippedMigrations = skippedMigrations
            });

            bool IsCoveredByBaseline(Migration migration) => migration.Version != null && baselineVersion != null &&
                                                             string.Compare(migration.Version, baselineVersion, StringComparison.OrdinalIgnoreCase) <= 0;
        }, cancellationToken);
    }
}