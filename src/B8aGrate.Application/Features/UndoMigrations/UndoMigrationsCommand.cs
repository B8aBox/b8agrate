using B8aGrate.Application.Features.Abstract;
using B8aGrate.Domain.Projections;
using MediatR;
using YuckQi.Domain.Validation;

namespace B8aGrate.Application.Features.UndoMigrations;

public sealed class UndoMigrationsCommand : MigrationRequestBase, IRequest<Result<UndoMigrationsProjection>>
{
    public bool IsDryRun { get; init; }

    public int? Steps { get; init; }

    public string? TargetVersion { get; init; }
}