using B8aGrate.Domain.Entities;
using B8aGrate.Domain.ValueObjects;
using YuckQi.Domain.Validation;

namespace B8aGrate.Domain.Projections;

public sealed class MigrationSnapshotProjection
{
    public ICollection<Migration> AppliedMigrations { get; set; } = [];

    public string? CurrentVersion { get; set; }

    public ICollection<ResultDetail> Errors { get; set; } = [];

    public ICollection<Migration> PendingMigrations { get; set; } = [];

    public ProviderType Provider { get; set; }
}