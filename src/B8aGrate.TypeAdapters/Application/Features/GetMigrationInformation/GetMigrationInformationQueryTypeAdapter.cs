using B8aGrate.Application.Features.GetMigrationInformation;
using Mapster;

namespace B8aGrate.TypeAdapters.Application.Features.GetMigrationInformation;

public sealed class GetMigrationInformationQueryTypeAdapter : IRegister
{
    public void Register(TypeAdapterConfig config) => config.ForType<Tuple<Dictionary<string, string?>, string>, GetMigrationInformationQuery>()
                                                            .Map(d => d.ConnectionString,
                                                                s => s.Item1.GetValueOrDefault("connection") ?? s.Item1.GetValueOrDefault("c"))
                                                            .Map(d => d.Root, s => s.Item2);
}