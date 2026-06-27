namespace B8aGrate.Domain.ValueObjects;

public enum MigrationKind
{
    Adopted,

    Baseline,

    Provision,

    ProvisionUndo,

    Repeatable,

    Undo,

    Versioned
}