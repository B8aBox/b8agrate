using B8aGrate.Domain.ValueObjects;

namespace B8aGrate.Data.Services.Interfaces;

public interface IMigrationVersionProvider
{
    string GetNextVersion(string path, VersioningOptions options);
}