using B8aGrate.Application.Features.Abstract;
using MediatR;
using YuckQi.Domain.Validation;

namespace B8aGrate.Application.Features.CreateBaseline;

public sealed class CreateBaselineCommand : MigrationRequestBase, IRequest<Result>
{
    public required string Description { get; init; }

    public required string Version { get; init; }
}