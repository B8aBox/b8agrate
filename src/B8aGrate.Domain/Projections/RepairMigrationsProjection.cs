using B8aGrate.Domain.Entities;
using YuckQi.Domain.Validation;

namespace B8aGrate.Domain.Projections;

public sealed class RepairMigrationsProjection
{
    public ICollection<Migration> FailedMigrations { get; set; } = [];

    public ICollection<ResultDetail> Errors { get; set; } = [];

    public ICollection<Migration> InvalidMigrations { get; set; } = [];

    public ICollection<string> Messages { get; set; } = [];
}