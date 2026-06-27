using B8aGrate.Application.Features.Abstract;
using B8aGrate.Domain.Projections;
using MediatR;
using YuckQi.Domain.Validation;

namespace B8aGrate.Application.Features.RepairMigrations;

public sealed class RepairMigrationsCommand : MigrationRequestBase, IRequest<Result<RepairMigrationsProjection>>
{
    public bool IsDryRun { get; init; }

    public bool ShouldRemoveFailed { get; init; }

    public bool ShouldUpdateChecksums { get; init; }
}