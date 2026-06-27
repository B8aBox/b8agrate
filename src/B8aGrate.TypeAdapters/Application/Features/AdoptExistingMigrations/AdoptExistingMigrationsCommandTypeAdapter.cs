using B8aGrate.Application.Features.AdoptExistingMigrations;
using Mapster;

namespace B8aGrate.TypeAdapters.Application.Features.AdoptExistingMigrations;

public sealed class AdoptExistingMigrationsCommandTypeAdapter : IRegister
{
    public void Register(TypeAdapterConfig config) => config.ForType<Tuple<Dictionary<string, string?>, string>, AdoptExistingMigrationsCommand>()
                                                            .Map(d => d.ConnectionString,
                                                                s => s.Item1.GetValueOrDefault("connection") ?? s.Item1.GetValueOrDefault("c"))
                                                            .Map(d => d.IsAll, s => s.Item1.ContainsKey("all") || s.Item1.ContainsKey("a"))
                                                            .Map(d => d.IsDryRun, s => s.Item1.ContainsKey("dry-run"))
                                                            .Map(d => d.Root, s => s.Item2)
                                                            .Map(d => d.TargetVersion,
                                                                s => s.Item1.GetValueOrDefault("target") ?? s.Item1.GetValueOrDefault("t"));
}