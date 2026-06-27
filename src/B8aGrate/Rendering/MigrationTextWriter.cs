using B8aGrate.Domain.Entities;
using YuckQi.Domain.Validation;

namespace B8aGrate.Rendering;

internal static class MigrationTextWriter
{
    #region Internal Methods

    internal static void RenderErrors(string title, IEnumerable<ResultDetail> errors, TextWriter writer)
    {
        var items = errors.ToArray();

        if (items.Length == 0)
            return;

        writer.WriteLine(title);

        foreach (var error in items)
        {
            var code = error.Code?.ToString();

            writer.WriteLine(string.IsNullOrWhiteSpace(code) ? $"  {error.Message}" : $"  {code}: {error.Message}");
        }
    }

    internal static void RenderMigrations(string title, IEnumerable<Migration> migrations, TextWriter writer)
    {
        var items = migrations.ToArray();

        if (items.Length == 0)
            return;

        writer.WriteLine(title);

        foreach (var migration in items)
            writer.WriteLine($"  {FormatMigration(migration)}");
    }

    #endregion


    #region Private Methods

    private static string FormatMigration(Migration migration)
    {
        var version = string.IsNullOrWhiteSpace(migration.Version) ? "-" : migration.Version;

        var execution = migration.ExecutionMoment == default && migration.ExecutionMilliseconds == 0
            ? string.Empty
            : $" ({(migration.IsSuccess ? "success" : "failed")}, {migration.ExecutionMilliseconds}ms)";

        return $"{migration.Kind,-13} {version,-16} {migration.Script}{execution}";
    }

    #endregion
}