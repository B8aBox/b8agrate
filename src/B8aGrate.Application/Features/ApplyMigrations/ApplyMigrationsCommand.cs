using B8aGrate.Application.Features.Abstract;
using B8aGrate.Domain.Projections;
using MediatR;
using YuckQi.Domain.Validation;

namespace B8aGrate.Application.Features.ApplyMigrations;

public sealed class ApplyMigrationsCommand : AdminMigrationRequestBase, IRequest<Result<ApplyMigrationsProjection>>
{
    public bool IsDryRun { get; init; }
}