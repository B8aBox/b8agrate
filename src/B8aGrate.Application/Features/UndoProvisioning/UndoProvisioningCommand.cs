using B8aGrate.Application.Features.Abstract;
using B8aGrate.Domain.Projections;
using MediatR;
using YuckQi.Domain.Validation;

namespace B8aGrate.Application.Features.UndoProvisioning;

public sealed class UndoProvisioningCommand : AdminMigrationRequestBase, IRequest<Result<UndoProvisioningProjection>>
{
    public bool IsDryRun { get; init; }
}