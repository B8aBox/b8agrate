using System.Text.RegularExpressions;
using B8aGrate.Data.Services.Interfaces;
using B8aGrate.Domain.ValueObjects;

namespace B8aGrate.Data.Services;

public sealed partial class MigrationVersionProvider : IMigrationVersionProvider
{
    #region Public Methods

    public string GetNextVersion(string path, VersioningOptions options) => options.Strategy switch
    {
        VersioningStrategy.Sequential => GetNextSequentialVersion(path, Math.Max(options.SequentialWidth ?? 6, 1)),
        VersioningStrategy.DateSequence => GetNextDateSequenceVersion(path),
        VersioningStrategy.Timestamp => DateTime.UtcNow.ToString("yyyyMMddHHmmss"),
        _ => throw new InvalidOperationException($"Unsupported versioning strategy: {options.Strategy}.")
    };

    #endregion


    #region Private Methods

    private static string GetNextDateSequenceVersion(string path)
    {
        var today = DateTime.UtcNow.ToString("yyyyMMdd");

        var maxForToday = Directory.Exists(path)
            ? Directory.EnumerateFiles(path, $"V{today}*__*.sql", SearchOption.TopDirectoryOnly)
                       .Select(x => TryExtractVersion(Path.GetFileName(x)))
                       .Where(x => x is not null && x.StartsWith(today, StringComparison.Ordinal) && x.Length == 10)
                       .Select(x => int.TryParse(x![8..], out var n) ? n : 0)
                       .DefaultIfEmpty(0)
                       .Max()
            : 0;

        return today + (maxForToday + 1).ToString("D2");
    }

    private static string GetNextSequentialVersion(string path, int width)
    {
        var max = Directory.Exists(path)
            ? Directory.EnumerateFiles(path, "V*__*.sql", SearchOption.TopDirectoryOnly)
                       .Select(x => TryExtractVersion(Path.GetFileName(x)))
                       .Where(x => int.TryParse(x, out _))
                       .Select(int.Parse!)
                       .DefaultIfEmpty(0)
                       .Max()
            : 0;

        return (max + 1).ToString($"D{width}");
    }

    private static string? TryExtractVersion(string fileName)
    {
        var match = VersionRegex().Match(fileName);

        return match.Success ? match.Groups["version"].Value : null;
    }

    [GeneratedRegex(@"^V(?<version>[^_]+)__.*\.sql$", RegexOptions.IgnoreCase)]
    private static partial Regex VersionRegex();

    #endregion
}