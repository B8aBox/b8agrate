using B8aGrate.Application.Extensions;
using B8aGrate.Application.Features.Abstract;
using B8aGrate.Data.Repositories.Interfaces;
using B8aGrate.Data.Services.Interfaces;
using B8aGrate.Domain.Projections;
using B8aGrate.Domain.Validation;
using MediatR;
using YuckQi.Domain.Validation;

namespace B8aGrate.Application.Features.RepairMigrations;

public sealed class RepairMigrationsCommandHandler(IMigrationScriptDiscoverer migrationScriptDiscoverer, IMigrationRepositoryFactory migrationRepositoryFactory)
    : MigrationHandlerBase<RepairMigrationsCommand, Result<RepairMigrationsProjection>>(migrationRepositoryFactory),
        IRequestHandler<RepairMigrationsCommand, Result<RepairMigrationsProjection>>
{
    public Task<Result<RepairMigrationsProjection>> Handle(RepairMigrationsCommand request, CancellationToken cancellationToken) => Handle(request,
        async (configuration, connection, migrationRepository) =>
        {
            var migrationsPath = Path.GetFullPath(Path.Combine(request.Root, configuration.Migration.Path));
            var migrationScripts = migrationScriptDiscoverer.Discover(migrationsPath);

            using var migrationLock = await migrationRepository.AcquireLock(connection, cancellationToken);
            var migrations = await migrationRepository.GetMigrationList(connection, cancellationToken);

            var projection = new RepairMigrationsProjection();

            foreach (var discoveryError in migrationScripts.GetDiscoveryErrors())
                projection.Errors.Add(discoveryError);

            if (projection.Errors.Count > 0 && !request.IsDryRun && (request.ShouldRemoveFailed || request.ShouldUpdateChecksums))
                return Results.Success(projection);

            if (request.ShouldRemoveFailed)
            {
                if (request.IsDryRun)
                {
                    projection.FailedMigrations = migrations.Where(x => !x.IsSuccess).ToList();
                }
                else
                {
                    var deleted = await migrationRepository.DeleteFailedRows(connection);

                    projection.Messages.Add($"Deleted {deleted} failed migrations.");
                }
            }

            var enrichedMigrationScripts = await migrationScripts.EnrichMigrationScripts(migrationsPath, cancellationToken);
            var migrationScriptsByName = enrichedMigrationScripts.ToDictionary(x => x.Script, StringComparer.OrdinalIgnoreCase);

            foreach (var migration in migrations)
            {
                if (!migrationScriptsByName.TryGetValue(migration.Script, out var migrationScript))
                {
                    projection.Errors.Add(new ResultDetail($"Cannot repair missing script {migration.Script}.", "MissingScript"));

                    continue;
                }

                if (string.Equals(migration.Checksum, migrationScript.Checksum, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (request.ShouldUpdateChecksums)
                {
                    if (request.IsDryRun)
                    {
                        projection.InvalidMigrations.Add(migration);
                    }
                    else
                    {
                        await migrationRepository.UpdateChecksum(connection, migration.Id,
                            migrationScript.Checksum ?? throw new InvalidOperationException("Checksum cannot be retrieved."));

                        projection.Messages.Add($"Updated checksum for {migration.Script}.");
                    }
                }
                else
                {
                    projection.Errors.Add(new ResultDetail($"Checksum differs for {migration.Script}. Use --update-checksums to repair.", "ChecksumMismatch"));
                }
            }

            return Results.Success(projection);
        }, cancellationToken);
}