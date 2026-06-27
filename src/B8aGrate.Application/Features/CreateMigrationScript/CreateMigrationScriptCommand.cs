using B8aGrate.Application.Features.Abstract;
using MediatR;
using YuckQi.Domain.Validation;

namespace B8aGrate.Application.Features.CreateMigrationScript;

public sealed class CreateMigrationScriptCommand : RequestBase, IRequest<Result>
{
    public bool HasUndo { get; init; }

    public bool IsProvision { get; init; }

    public bool IsRepeatable { get; init; }

    public required string Name { get; init; }
}