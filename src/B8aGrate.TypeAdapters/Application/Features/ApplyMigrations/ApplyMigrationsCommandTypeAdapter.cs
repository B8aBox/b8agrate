using B8aGrate.Application.Features.ApplyMigrations;
using Mapster;

namespace B8aGrate.TypeAdapters.Application.Features.ApplyMigrations;

public sealed class ApplyMigrationsCommandTypeAdapter : IRegister
{
    public void Register(TypeAdapterConfig config) => config.ForType<Tuple<Dictionary<string, string?>, string>, ApplyMigrationsCommand>()
                                                            .Map(d => d.AdminConnectionString, s => s.Item1.GetValueOrDefault("admin-connection"))
                                                            .Map(d => d.ConnectionString,
                                                                s => s.Item1.GetValueOrDefault("connection") ?? s.Item1.GetValueOrDefault("c"))
                                                            .Map(d => d.IsDryRun, s => s.Item1.ContainsKey("dry-run"))
                                                            .Map(d => d.Root, s => s.Item2);
}