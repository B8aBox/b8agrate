using B8aGrate.Application.Features.CreateBaseline;
using Mapster;

namespace B8aGrate.TypeAdapters.Application.Features.CreateBaseline;

public sealed class CreateBaselineCommandTypeAdapter : IRegister
{
    public void Register(TypeAdapterConfig config) => config.ForType<Tuple<Dictionary<string, string?>, string>, CreateBaselineCommand>()
                                                            .Map(d => d.ConnectionString,
                                                                s => s.Item1.GetValueOrDefault("connection") ?? s.Item1.GetValueOrDefault("c"))
                                                            .Map(d => d.Description,
                                                                s => s.Item1.GetValueOrDefault("description") ??
                                                                     s.Item1.GetValueOrDefault("d") ?? "Creating database baseline.")
                                                            .Map(d => d.Root, s => s.Item2)
                                                            .Map(d => d.Version, s => s.Item1.GetValueOrDefault("version") ?? s.Item1.GetValueOrDefault("v"));
}