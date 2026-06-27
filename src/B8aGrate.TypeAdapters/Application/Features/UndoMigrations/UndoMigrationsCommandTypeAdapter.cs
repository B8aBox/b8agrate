using B8aGrate.Application.Features.UndoMigrations;
using Mapster;

namespace B8aGrate.TypeAdapters.Application.Features.UndoMigrations;

public sealed class UndoMigrationsCommandTypeAdapter : IRegister
{
    public void Register(TypeAdapterConfig config) => config.ForType<Tuple<Dictionary<string, string?>, string>, UndoMigrationsCommand>()
                                                            .Map(d => d.ConnectionString,
                                                                s => s.Item1.GetValueOrDefault("connection") ?? s.Item1.GetValueOrDefault("c"))
                                                            .Map(d => d.IsDryRun, s => s.Item1.ContainsKey("dry-run"))
                                                            .Map(d => d.Root, s => s.Item2)
                                                            .Map(d => d.Steps, s => s.Item1.GetValueOrDefault("steps") ?? s.Item1.GetValueOrDefault("s"))
                                                            .Map(d => d.TargetVersion,
                                                                s => s.Item1.GetValueOrDefault("target") ?? s.Item1.GetValueOrDefault("t"));
}