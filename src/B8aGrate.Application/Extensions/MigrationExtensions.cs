using B8aGrate.Domain.Entities;
using B8aGrate.Domain.ValueObjects;
using YuckQi.Domain.Validation;

namespace B8aGrate.Application.Extensions;

public static class MigrationExtensions
{
    #region Public Methods

    public static async Task<List<Migration>> EnrichMigrationScripts(this IEnumerable<Migration> migrationScripts, string migrationsPath,
        CancellationToken cancellationToken)
    {
        var tasks = migrationScripts.Select(async migration =>
        {
            var path = Path.Combine(migrationsPath, migration.Script);
            var sql = await File.ReadAllTextAsync(path, cancellationToken);

            migration.Checksum = sql.GetChecksum();
            migration.Sql = sql;

            return migration;
        });

        return (await Task.WhenAll(tasks)).ToList();
    }

    extension(IReadOnlyList<Migration> migrations)
    {
        public HashSet<string> GetAppliedVersions()
        {
            var applied = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var row in migrations.Where(x => x.IsSuccess).OrderBy(x => x.Id))
            {
                if (row.Kind is MigrationKind.Versioned or MigrationKind.Adopted && row.Version is not null)
                    applied.Add(row.Version);

                if (row is { Kind: MigrationKind.Undo, Version: not null })
                    applied.Remove(row.Version);

                if (row is { Kind: MigrationKind.Baseline, Version: not null })
                    applied.Add(row.Version);
            }

            return applied;
        }

        public string? GetBaselineVersion() => migrations
                                               .Where(x => x is { IsSuccess: true, Kind: MigrationKind.Baseline })
                                               .OrderByDescending(x => x.Id)
                                               .Select(x => x.Version)
                                               .FirstOrDefault();

        public Dictionary<string, Migration> GetLatestSuccessfulRepeatableMigrations() => migrations
                                                                                          .Where(x => x is { IsSuccess: true, Kind: MigrationKind.Repeatable })
                                                                                          .GroupBy(x => x.Script)
                                                                                          .ToDictionary(x => x.Key, x => x.OrderByDescending(y => y.Id).First(),
                                                                                              StringComparer.OrdinalIgnoreCase);

        public List<Migration> GetSuccessfulMigrations() => migrations.Where(x => x.IsSuccess).OrderBy(x => x.Id).ToList();

        public List<ResultDetail> GetFailedMigrationErrors() => migrations
                                                                .Where(x => !x.IsSuccess)
                                                                .OrderBy(x => x.Id)
                                                                .Select(x => new ResultDetail($"Failed migration recorded: {x.Script}", "FailedMigration"))
                                                                .ToList();

        public bool IsProvisionApplied() => migrations.Where(x => x.IsSuccess)
                                                      .OrderBy(x => x.Id)
                                                      .Aggregate(false, (current, row) => row.Kind switch
                                                      {
                                                          MigrationKind.Provision => true,
                                                          MigrationKind.ProvisionUndo => false,
                                                          _ => current
                                                      });

        public List<ResultDetail> Validate(Dictionary<string, Migration> scriptsByName)
        {
            var errors = new List<ResultDetail>();

            foreach (var row in migrations.Where(x => x is
                     {
                         IsSuccess: true,
                         Kind: MigrationKind.Versioned or MigrationKind.Repeatable or MigrationKind.Adopted or MigrationKind.Provision
                         or MigrationKind.ProvisionUndo
                     }).OrderBy(x => x.Id))
            {
                if (!scriptsByName.TryGetValue(row.Script, out var script))
                {
                    errors.Add(new ResultDetail($"Applied script is missing: {row.Script}", "MissingScript"));

                    continue;
                }

                if (!string.Equals(row.Checksum, script.Checksum, StringComparison.OrdinalIgnoreCase))
                    errors.Add(new ResultDetail($"Checksum changed for {row.Script}", "ChecksumChanged"));
            }

            return errors;
        }
    }

    extension(IReadOnlyList<Migration> migrationScripts)
    {
        public List<ResultDetail> GetDiscoveryErrors()
        {
            var errors = new List<ResultDetail>();

            errors.AddRange(GetDuplicateVersionErrors(migrationScripts));
            errors.AddRange(GetDuplicateProvisionErrors(migrationScripts));
            errors.AddRange(GetDuplicateProvisionUndoErrors(migrationScripts));

            return errors;
        }

        public List<Migration> GetPendingProvisionScripts(bool isProvisionApplied) => isProvisionApplied
            ? []
            : migrationScripts.Where(x => x.Kind == MigrationKind.Provision).OrderBy(x => x.Script, StringComparer.OrdinalIgnoreCase).ToList();

        public async Task<List<Migration>> GetPendingRepeatableMigrationScripts(Dictionary<string, Migration> repeatableMigrations, string migrationsPath,
            CancellationToken cancellationToken)
        {
            var enrichedRepeatableScripts = await migrationScripts.Where(x => x.Kind == MigrationKind.Repeatable)
                                                                  .EnrichMigrationScripts(migrationsPath, cancellationToken);

            return enrichedRepeatableScripts.GetPendingRepeatableMigrationScripts(repeatableMigrations);
        }

        public List<Migration> GetPendingRepeatableMigrationScripts(Dictionary<string, Migration> repeatableMigrations) =>
            migrationScripts
                .Where(x => x.Kind == MigrationKind.Repeatable)
                .Where(x => !repeatableMigrations.TryGetValue(x.Script, out var row) ||
                            !string.Equals(row.Checksum, x.Checksum, StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => x.Script, StringComparer.OrdinalIgnoreCase)
                .ToList();

        public List<Migration> GetPendingVersionedMigrationScripts(HashSet<string> appliedVersions, string? baselineVersion) => migrationScripts
            .Where(x => x.Kind == MigrationKind.Versioned)
            .Where(x => baselineVersion == null || string.Compare(x.Version, baselineVersion, StringComparison.OrdinalIgnoreCase) > 0)
            .Where(x => !appliedVersions.Contains(x.Version!))
            .ToList();
    }

    #endregion


    #region Private Methods

    private static List<ResultDetail> GetDuplicateProvisionErrors(IReadOnlyList<Migration> migrationScripts)
    {
        var provisionScripts = migrationScripts
                               .Where(x => x.Kind == MigrationKind.Provision)
                               .OrderBy(x => x.Script, StringComparer.OrdinalIgnoreCase)
                               .ToList();

        return provisionScripts.Count <= 1
            ? []
            :
            [
                new ResultDetail(
                    $"Only one provision script is allowed: {string.Join(", ", provisionScripts.Select(x => x.Script))}.",
                    "MultipleProvisionScripts")
            ];
    }

    private static List<ResultDetail> GetDuplicateProvisionUndoErrors(IReadOnlyList<Migration> migrationScripts)
    {
        var provisionUndoScripts = migrationScripts
                                   .Where(x => x.Kind == MigrationKind.ProvisionUndo)
                                   .OrderBy(x => x.Script, StringComparer.OrdinalIgnoreCase)
                                   .ToList();

        return provisionUndoScripts.Count <= 1
            ? []
            :
            [
                new ResultDetail(
                    $"Only one provision undo script is allowed: {string.Join(", ", provisionUndoScripts.Select(x => x.Script))}.",
                    "MultipleProvisionUndoScripts")
            ];
    }

    private static List<ResultDetail> GetDuplicateVersionErrors(IReadOnlyList<Migration> migrationScripts) =>
        migrationScripts
            .Where(x => x.Kind == MigrationKind.Versioned)
            .GroupBy(x => x.Version, StringComparer.OrdinalIgnoreCase)
            .Where(x => x.Key is not null && x.Count() > 1)
            .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .Select(duplicate =>
            {
                var scriptNames = string.Join(", ", duplicate
                                                    .Select(x => x.Script)
                                                    .OrderBy(x => x, StringComparer.OrdinalIgnoreCase));

                return new ResultDetail(
                    $"Multiple V scripts have version {duplicate.Key}: {scriptNames}",
                    "DuplicateVersion");
            })
            .ToList();

    #endregion
}