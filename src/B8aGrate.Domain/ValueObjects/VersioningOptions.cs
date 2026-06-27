namespace B8aGrate.Domain.ValueObjects;

public sealed class VersioningOptions
{
    public VersioningStrategy Strategy { get; set; } = VersioningStrategy.Sequential;

    public int? SequentialWidth { get; set; }
}