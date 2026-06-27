using B8aGrate.Application.Features.CreateMigrationScript;
using Mapster;

namespace B8aGrate.TypeAdapters.Application.Features.CreateMigrationScript;

public sealed class CreateMigrationScriptCommandTypeAdapter : IRegister
{
    public void Register(TypeAdapterConfig config) => config.ForType<Tuple<Dictionary<string, string?>, string>, CreateMigrationScriptCommand>()
                                                            .Map(d => d.HasUndo, s => !s.Item1.ContainsKey("no-undo"))
                                                            .Map(d => d.IsProvision, s => s.Item1.ContainsKey("provision") || s.Item1.ContainsKey("p"))
                                                            .Map(d => d.IsRepeatable, s => s.Item1.ContainsKey("repeatable") || s.Item1.ContainsKey("r"))
                                                            .Map(d => d.Name, s => s.Item1.GetValueOrDefault("name") ?? s.Item1.GetValueOrDefault("n"))
                                                            .Map(d => d.Root, s => s.Item2);
}