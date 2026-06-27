using B8aGrate.Application.Features.Abstract;
using B8aGrate.Domain.Projections;
using MediatR;
using YuckQi.Domain.Validation;

namespace B8aGrate.Application.Features.AdoptExistingMigrations;

public class AdoptExistingMigrationsCommand : MigrationRequestBase, IRequest<Result<AdoptExistingMigrationsProjection>>
{
    public bool IsAll { get; init; }

    public bool IsDryRun { get; init; }

    public string? TargetVersion { get; init; }
}