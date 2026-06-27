using B8aGrate.Application.Features.InitializeProject;
using B8aGrate.Domain.ValueObjects;
using Mapster;

namespace B8aGrate.TypeAdapters.Application.Features.InitializeProject;

public sealed class InitializeProjectCommandTypeAdapter : IRegister
{
    #region Public Methods

    public void Register(TypeAdapterConfig config) => config.ForType<Tuple<Dictionary<string, string?>, string>, InitializeProjectCommand>()
                                                            .Map(d => d.Path, s => s.Item1.GetValueOrDefault("path"))
                                                            .Map(d => d.Project, s => s.Item1.GetValueOrDefault("project"))
                                                            .Map(d => d.Provider, s => GetProvider(s.Item1.GetValueOrDefault("provider")))
                                                            .Map(d => d.Root, s => s.Item2)
                                                            .Map(d => d.WithReadme,
                                                                s => s.Item1.ContainsKey("with-readme") || s.Item1.ContainsKey("full") ||
                                                                     s.Item1.ContainsKey("f"))
                                                            .Map(d => d.WithTemplates,
                                                                s => s.Item1.ContainsKey("with-templates") || s.Item1.ContainsKey("full") ||
                                                                     s.Item1.ContainsKey("f"));

    #endregion


    #region Private Methods

    private static ProviderType? GetProvider(string? provider) => provider?.ToLowerInvariant() switch
    {
        "sqlserver" or "mssql" => ProviderType.SqlServer,
        "postgres" or "postgresql" or "pg" => ProviderType.PostgreSql,
        _ => null
    };

    #endregion
}