using B8aGrate.Application.Extensions;
using B8aGrate.Application.Features.Abstract;
using B8aGrate.Data.Repositories.Interfaces;
using B8aGrate.Data.Services.Interfaces;
using B8aGrate.Domain.Validation;
using MediatR;
using YuckQi.Domain.Validation;

namespace B8aGrate.Application.Features.ValidateMigrationHistory;

public sealed class ValidateMigrationHistoryCommandHandler(
    IMigrationScriptDiscoverer migrationScriptDiscoverer, IMigrationRepositoryFactory migrationRepositoryFactory)
    : MigrationHandlerBase<ValidateMigrationHistoryCommand, Result>(migrationRepositoryFactory), IRequestHandler<ValidateMigrationHistoryCommand, Result>
{
    public Task<Result> Handle(ValidateMigrationHistoryCommand request, CancellationToken cancellationToken) => Handle(request,
        async (configuration, connection, migrationRepository) =>
        {
            var migrations = await migrationRepository.GetMigrationList(connection, cancellationToken);
            var migrationsPath = Path.GetFullPath(Path.Combine(request.Root, configuration.Migration.Path));
            var migrationScripts = migrationScriptDiscoverer.Discover(migrationsPath);
            var discoveryErrors = migrationScripts.GetDiscoveryErrors();
            var enrichedMigrationScripts = await migrationScripts.EnrichMigrationScripts(migrationsPath, cancellationToken);
            
            discoveryErrors.AddRange(migrations.Validate(enrichedMigrationScripts.ToDictionary(x => x.Script, StringComparer.OrdinalIgnoreCase)));

            return discoveryErrors.Count != 0 ? Results.Failure(discoveryErrors) : Results.Success();
        }, cancellationToken);
}