using B8aGrate.Application.Features.GetMigrationSnapshot;
using Mapster;

namespace B8aGrate.TypeAdapters.Application.Features.GetMigrationSnapshot;

public sealed class GetMigrationSnapshotQueryTypeAdapter : IRegister
{
    public void Register(TypeAdapterConfig config) => config.ForType<Tuple<Dictionary<string, string?>, string>, GetMigrationSnapshotQuery>()
                                                            .Map(d => d.ConnectionString,
                                                                s => s.Item1.GetValueOrDefault("connection") ?? s.Item1.GetValueOrDefault("c"))
                                                            .Map(d => d.Root, s => s.Item2);
}