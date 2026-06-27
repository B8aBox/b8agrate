namespace B8aGrate.Domain.ValueObjects;

public sealed class MigrationOptions
{
    public string Path { get; set; } = "./data/migrations";

    public string Schema { get; set; } = "b8agrate";

    public string Table { get; set; } = "Migration";
}