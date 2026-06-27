using B8aGrate.Application.Features.Abstract;
using B8aGrate.Data.Repositories.Interfaces;
using B8aGrate.Domain.Entities;
using B8aGrate.Domain.Validation;
using B8aGrate.Domain.ValueObjects;
using MediatR;
using YuckQi.Domain.Validation;

namespace B8aGrate.Application.Features.CreateBaseline;

public sealed class CreateBaselineCommandHandler(IMigrationRepositoryFactory migrationRepositoryFactory)
    : MigrationHandlerBase<CreateBaselineCommand, Result>(migrationRepositoryFactory), IRequestHandler<CreateBaselineCommand, Result>
{
    public Task<Result> Handle(CreateBaselineCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Version))
            return Task.FromResult(Results.Failure("Missing --version or --v."));

        return Handle(request, async (_, connection, migrationRepository) =>
        {
            using var migrationLock = await migrationRepository.AcquireLock(connection, cancellationToken);
            var migrations = await migrationRepository.GetMigrationList(connection, cancellationToken);

            if (migrations.Any(x => x.IsSuccess))
                return Results.Failure("Baseline can only be created when the migration table is empty.");

            var migration = new Migration
            {
                Description = request.Description,
                ExecutionMoment = DateTimeOffset.UtcNow,
                IsSuccess = true,
                Kind = MigrationKind.Baseline,
                Script = "<< baseline >>",
                Version = request.Version
            };

            await migrationRepository.InsertMigration(connection, migration, cancellationToken);

            return Results.Success();
        }, cancellationToken);
    }
}