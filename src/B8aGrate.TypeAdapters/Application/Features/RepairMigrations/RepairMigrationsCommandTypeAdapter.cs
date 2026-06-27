using B8aGrate.Application.Features.RepairMigrations;
using Mapster;

namespace B8aGrate.TypeAdapters.Application.Features.RepairMigrations;

public sealed class RepairMigrationsCommandTypeAdapter : IRegister
{
    public void Register(TypeAdapterConfig config) => config.ForType<Tuple<Dictionary<string, string?>, string>, RepairMigrationsCommand>()
                                                            .Map(d => d.ConnectionString,
                                                                s => s.Item1.GetValueOrDefault("connection") ?? s.Item1.GetValueOrDefault("c"))
                                                            .Map(d => d.IsDryRun, s => s.Item1.ContainsKey("dry-run"))
                                                            .Map(d => d.Root, s => s.Item2)
                                                            .Map(d => d.ShouldRemoveFailed, s => s.Item1.ContainsKey("remove-failed"))
                                                            .Map(d => d.ShouldUpdateChecksums, s => s.Item1.ContainsKey("update-checksums"));
}