using B8aGrate.Domain.ValueObjects;
using MediatR;
using YuckQi.Domain.Validation;

namespace B8aGrate.Application.Features.InitializeProject;

public sealed class InitializeProjectCommand : IRequest<Result>
{
    public string? Path { get; init; }

    public string? Project { get; init; }

    public ProviderType? Provider { get; init; }

    public required string Root { get; init; }

    public bool WithReadme { get; init; }

    public bool WithTemplates { get; init; }
}