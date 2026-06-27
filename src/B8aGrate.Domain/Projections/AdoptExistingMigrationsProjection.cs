using B8aGrate.Domain.Entities;
using YuckQi.Domain.Validation;

namespace B8aGrate.Domain.Projections;

public sealed class AdoptExistingMigrationsProjection
{
    public ICollection<Migration> AppliedMigrations { get; set; } = [];

    public ICollection<ResultDetail> Errors { get; set; } = [];

    public string? Message { get; set; }

    public ICollection<Migration> SkippedMigrations { get; set; } = [];
}