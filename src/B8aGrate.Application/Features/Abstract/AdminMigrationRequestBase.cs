namespace B8aGrate.Application.Features.Abstract;

public abstract class AdminMigrationRequestBase : MigrationRequestBase
{
    public string? AdminConnectionString { get; init; }
}