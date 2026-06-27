using B8aGrate.Application.Features.ValidateMigrationHistory;
using Mapster;

namespace B8aGrate.TypeAdapters.Application.Features.ValidateMigrationHistory;

public sealed class ValidateMigrationHistoryCommandTypeAdapter : IRegister
{
    public void Register(TypeAdapterConfig config) => config.ForType<Tuple<Dictionary<string, string?>, string>, ValidateMigrationHistoryCommand>()
                                                            .Map(d => d.ConnectionString,
                                                                s => s.Item1.GetValueOrDefault("connection") ?? s.Item1.GetValueOrDefault("c"))
                                                            .Map(d => d.Root, s => s.Item2);
}