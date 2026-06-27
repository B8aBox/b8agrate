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

namespace B8aGrate.Application.Features.UndoMigrations;

public sealed class UndoMigrationsCommandHandler(IMigrationScriptDiscoverer migrationScriptDiscoverer, IMigrationRepositoryFactory migrationRepositoryFactory)
    : MigrationHandlerBase<UndoMigrationsCommand, Result<UndoMigrationsProjection>>(migrationRepositoryFactory),
        IRequestHandler<UndoMigrationsCommand, Result<UndoMigrationsProjection>>
{
    public Task<Result<UndoMigrationsProjection>> Handle(UndoMigrationsCommand request, CancellationToken cancellationToken) => Handle(request,
        async (configuration, connection, migrationRepository) =>
        {
            var migrationsPath = Path.GetFullPath(Path.Combine(request.Root, configuration.Migration.Path));
            var migrationScripts = migrationScriptDiscoverer.Discover(migrationsPath);
            var discoveryErrors = migrationScripts.GetDiscoveryErrors();

            if (discoveryErrors.Count > 0)
                return Results.Failure<UndoMigrationsProjection>(discoveryErrors);

            var undoMigrationScriptsByVersion = migrationScripts
                                                .Where(x => x.Kind == MigrationKind.Undo)
                                                .ToDictionary(x => x.Version!, StringComparer.OrdinalIgnoreCase);

            using var migrationLock = await migrationRepository.AcquireLock(connection, cancellationToken);

            var migrations = await migrationRepository.GetMigrationList(connection, cancellationToken);
            var appliedVersions = migrations.GetAppliedVersions();

            var appliedMigrations = migrations
                                    .Where(x => x is { IsSuccess: true, Kind: MigrationKind.Versioned or MigrationKind.Adopted } &&
                                                appliedVersions.Contains(x.Version!))
                                    .GroupBy(x => x.Version, StringComparer.OrdinalIgnoreCase)
                                    .Select(x => x.OrderByDescending(y => y.Id).First())
                                    .OrderByDescending(x => x.Version, StringComparer.OrdinalIgnoreCase)
                                    .ToList();

            var pendingUndoMigrations = request.Steps != null
                ? appliedMigrations.Take(Math.Max(request.Steps.Value, 0)).ToList()
                : string.IsNullOrWhiteSpace(request.TargetVersion)
                    ? appliedMigrations.Take(1).ToList()
                    : appliedMigrations.TakeWhile(x => string.Compare(x.Version, request.TargetVersion, StringComparison.OrdinalIgnoreCase) > 0).ToList();

            if (pendingUndoMigrations.Count == 0)
                return Results.Success(new UndoMigrationsProjection
                {
                    Message = "No migrations to undo."
                });

            var appliedUndoMigrations = new List<Migration>();

            foreach (var migration in pendingUndoMigrations)
            {
                if (!undoMigrationScriptsByVersion.TryGetValue(migration.Version!, out var migrationScript))
                    return Results.Failure<UndoMigrationsProjection>(
                        $"No undo script found for version {migration.Version}. Expected U{migration.Version}__*.sql", "MissingUndoScript");

                appliedUndoMigrations.Add(await RunMigrationScript(migrationRepository, connection, migrationScript, migrationsPath, request.IsDryRun,
                    cancellationToken));
            }

            return Results.Success(new UndoMigrationsProjection
            {
                AppliedMigrations = appliedUndoMigrations,
                Message = "Undo scripts are recorded as Undo rows. The original Versioned rows are preserved for audit history."
            });
        }, cancellationToken);
}