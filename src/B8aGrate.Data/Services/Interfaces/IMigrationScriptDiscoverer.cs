using B8aGrate.Domain.Entities;

namespace B8aGrate.Data.Services.Interfaces;

public interface IMigrationScriptDiscoverer
{
    IReadOnlyList<Migration> Discover(string path);
}