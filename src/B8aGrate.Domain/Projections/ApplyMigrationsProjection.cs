using B8aGrate.Domain.Entities;

namespace B8aGrate.Domain.Projections;

public sealed class ApplyMigrationsProjection
{
    public ICollection<Migration> AppliedMigrations { get; set; } = [];

    public string? Message { get; set; }

    public ICollection<Migration> SkippedMigrations { get; set; } = [];
}