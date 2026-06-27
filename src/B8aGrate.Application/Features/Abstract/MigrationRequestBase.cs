namespace B8aGrate.Application.Features.Abstract;

public abstract class MigrationRequestBase : RequestBase
{
    public string? ConnectionString { get; init; }
}