using System.Text.RegularExpressions;
using B8aGrate.Data.Services.Interfaces;
using B8aGrate.Domain.Entities;
using B8aGrate.Domain.ValueObjects;

namespace B8aGrate.Data.Services;

public sealed partial class MigrationScriptDiscoverer : IMigrationScriptDiscoverer
{
    #region Public Methods

    public IReadOnlyList<Migration> Discover(string path)
    {
        if (!Directory.Exists(path))
            throw new DirectoryNotFoundException(path);

        var files = Directory.EnumerateFiles(path, "*.sql", SearchOption.TopDirectoryOnly).ToList();
        var migrationScripts = files.Select(Parse).Where(x => x != null).Cast<Migration>().ToList();

        return migrationScripts;
    }

    #endregion


    #region Private Methods

    [GeneratedRegex(@"^R__(?<description>.+)\.sql$", RegexOptions.IgnoreCase | RegexOptions.Compiled, "en-US")]
    private static partial Regex RepeatableRegex();

    [GeneratedRegex(@"^PU__(?<description>.+)\.sql$", RegexOptions.IgnoreCase | RegexOptions.Compiled, "en-US")]
    private static partial Regex ProvisionUndoRegex();

    [GeneratedRegex(@"^P__(?<description>.+)\.sql$", RegexOptions.IgnoreCase | RegexOptions.Compiled, "en-US")]
    private static partial Regex ProvisionRegex();

    [GeneratedRegex(@"^U(?<version>[A-Za-z0-9_.-]+?)__(?<description>.+)\.sql$", RegexOptions.IgnoreCase | RegexOptions.Compiled, "en-US")]
    private static partial Regex UndoRegex();

    [GeneratedRegex(@"^V(?<version>[A-Za-z0-9_.-]+?)__(?<description>.+)\.sql$", RegexOptions.IgnoreCase | RegexOptions.Compiled, "en-US")]
    private static partial Regex VersionedRegex();

    [GeneratedRegex(@"\s+", RegexOptions.Compiled, "en-US")]
    private static partial Regex WhitespaceRegex();

    private static string NormalizeDescription(string description) => WhitespaceRegex().Replace(description.Replace('_', ' '), " ").Trim();

    private static Migration? Parse(string path)
    {
        var name = Path.GetFileName(path);
        var parsed = ParseName(name);

        if (parsed == null)
            return null;

        var (description, kind, version) = parsed.Value;

        return new Migration
        {
            Description = NormalizeDescription(description),
            Kind = kind,
            Script = name,
            Version = version
        };
    }

    private static (string description, MigrationKind kind, string? version)? ParseName(string name)
    {
        var match = ProvisionUndoRegex().Match(name);

        if (match.Success)
            return (match.Groups["description"].Value, MigrationKind.ProvisionUndo, null);

        match = ProvisionRegex().Match(name);

        if (match.Success)
            return (match.Groups["description"].Value, MigrationKind.Provision, null);

        match = VersionedRegex().Match(name);

        if (match.Success)
            return (match.Groups["description"].Value, MigrationKind.Versioned, match.Groups["version"].Value);

        match = UndoRegex().Match(name);

        if (match.Success)
            return (match.Groups["description"].Value, MigrationKind.Undo, match.Groups["version"].Value);

        match = RepeatableRegex().Match(name);

        return match.Success ? (match.Groups["description"].Value, MigrationKind.Repeatable, null) : null;
    }

    #endregion
}