using System.Reflection;
using B8aGrate.Data.Services.Interfaces;
using B8aGrate.Domain.Validation;
using B8aGrate.Domain.ValueObjects;
using MediatR;
using YuckQi.Domain.Validation;

namespace B8aGrate.Application.Features.InitializeProject;

public sealed class InitializeProjectCommandHandler(IMigrationVersionProvider migrationVersionProvider) : IRequestHandler<InitializeProjectCommand, Result>
{
    #region Public Methods

    public async Task<Result> Handle(InitializeProjectCommand request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.Path) && request.Path.Contains(".."))
            return Results.Failure("Path is not valid.");

        var configuration = new Configuration();

        if (!string.IsNullOrWhiteSpace(request.Path))
            configuration.Migration.Path = request.Path;

        if (request.Provider != null)
            configuration.Provider = request.Provider.Value;

        var path = Configuration.GetPath(request.Root);

        if (File.Exists(path))
            return Results.Failure($"Configuration already exists: {path}");

        await File.WriteAllTextAsync(path, configuration.ToJson() + Environment.NewLine, cancellationToken);

        var migrationsPath = Path.GetFullPath(Path.Combine(request.Root, configuration.Migration.Path));

        Directory.CreateDirectory(migrationsPath);

        await AddReadme(request.WithReadme, migrationsPath, cancellationToken);
        await AddTemplates(request.WithTemplates, configuration, migrationsPath, cancellationToken);

        var buildTargetResult = await AddBuildTarget(request.Project, cancellationToken);

        return !buildTargetResult.IsValid ? buildTargetResult : Results.Success();
    }

    #endregion


    #region Private Methods

    private static async Task<Result> AddBuildTarget(string? projectPath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(projectPath))
            return Results.Success();

        if (!File.Exists(projectPath))
            return Results.Failure("Project file was not found.");

        var xml = await File.ReadAllTextAsync(projectPath, cancellationToken);

        if (xml.Contains("Name=\"B8aGrateMigrate\"", StringComparison.OrdinalIgnoreCase))
            return Results.Success();

        const string target = """

                                <Target Name="B8aGrateMigrate" AfterTargets="Build" Condition="'$(Configuration)' == 'Debug'">
                                  <Exec Command="b8agrate migrate" />
                                </Target>
                              """;

        xml = xml.Replace("</Project>", target + Environment.NewLine + "</Project>");

        await File.WriteAllTextAsync(projectPath, xml, cancellationToken);

        return Results.Success();
    }

    private static async Task AddReadme(bool includeReadme, string path, CancellationToken cancellationToken)
    {
        if (!includeReadme)
            return;

        var readmePath = Path.Combine(path, "README.md");

        if (File.Exists(readmePath))
            return;

        await File.WriteAllTextAsync(readmePath, """
                                                 # b8agrate migrations

                                                 ## Common commands

                                                 ```bash
                                                 b8agrate add --name create_users
                                                 b8agrate migrate --connection "..."
                                                 b8agrate info --connection "..."
                                                 b8agrate undo --steps 1 --connection "..."
                                                 b8agrate unprovision --admin-connection "..."
                                                 ```

                                                 ## Naming

                                                 - `P__description.sql` runs before normal migrations with the admin connection.
                                                 - `PU__description.sql` undoes the provision script through `b8agrate unprovision`.
                                                 - `V000001__description.sql` runs once by default.
                                                 - `U000001__description.sql` undoes the matching version.
                                                 - `R__description.sql` reruns idempotent reference-data changes when the checksum changes.
                                                 """, cancellationToken);
    }

    private async Task AddTemplates(bool includeTemplates, Configuration configuration, string migrationsPath, CancellationToken cancellationToken)
    {
        if (!includeTemplates)
            return;

        var provider = configuration.Provider.ToString();
        var version = migrationVersionProvider.GetNextVersion(migrationsPath, configuration.Versioning);

        await WriteTemplateIfMissing(provider, "P_provision.sql", Path.Combine(migrationsPath, "P__bootstrap.sql"), "P__*.sql", cancellationToken);
        await WriteTemplateIfMissing(provider, "PU_provision.sql", Path.Combine(migrationsPath, "PU__undo_bootstrap.sql"), "PU__*.sql", cancellationToken);
        await WriteTemplate(provider, "V_initial_schema.sql", Path.Combine(migrationsPath, $"V{version}__initial_schema.sql"), cancellationToken);
        await WriteTemplate(provider, "U_initial_schema.sql", Path.Combine(migrationsPath, $"U{version}__drop_initial_schema.sql"), cancellationToken);
        await WriteTemplate(provider, "R_seed_countries.sql", Path.Combine(migrationsPath, "R__seed_countries.sql"), cancellationToken);
    }

    private static async Task WriteTemplate(string provider, string templateName, string destination, CancellationToken cancellationToken)
    {
        if (File.Exists(destination))
            return;

        var assembly = Assembly.GetExecutingAssembly();
        var suffix = $"Templates.{provider}.{templateName}";
        var resourceName = assembly.GetManifestResourceNames().SingleOrDefault(x => x.EndsWith(suffix, StringComparison.Ordinal));

        if (resourceName is null)
            throw new InvalidOperationException($"Missing embedded template: {suffix}.");

        await using var stream = assembly.GetManifestResourceStream(resourceName) ??
                                 throw new InvalidOperationException($"Could not open embedded template: {suffix}.");

        using var reader = new StreamReader(stream);

        await File.WriteAllTextAsync(destination, await reader.ReadToEndAsync(cancellationToken), cancellationToken);
    }

    private static async Task WriteTemplateIfMissing(string provider, string templateName, string destination, string existingPattern,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(destination) ?? Directory.GetCurrentDirectory();

        if (Directory.Exists(directory) && Directory.EnumerateFiles(directory, existingPattern, SearchOption.TopDirectoryOnly).Any())
            return;

        await WriteTemplate(provider, templateName, destination, cancellationToken);
    }

    #endregion
}