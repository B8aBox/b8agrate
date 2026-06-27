using System.Text.RegularExpressions;
using B8aGrate.Data.Services.Interfaces;
using B8aGrate.Domain.Validation;
using B8aGrate.Domain.ValueObjects;
using MediatR;
using YuckQi.Domain.Validation;

namespace B8aGrate.Application.Features.CreateMigrationScript;

public sealed partial class CreateMigrationScriptCommandHandler(IMigrationVersionProvider migrationVersionProvider)
    : IRequestHandler<CreateMigrationScriptCommand, Result>
{
    public async Task<Result> Handle(CreateMigrationScriptCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return Results.Failure("Missing --name.");

        var configuration = await Configuration.Load(request.Root, cancellationToken);
        var migrationsPath = Path.GetFullPath(Path.Combine(request.Root, configuration.Migration.Path));
        var name = NormalizeName(request.Name);

        if (request is { IsProvision: true, IsRepeatable: true })
            return Results.Failure("--provision cannot be combined with --repeatable.");

        if (request.IsProvision)
            return await AddProvisionScripts(migrationsPath, name, request.HasUndo, cancellationToken);

        if (request.IsRepeatable)
        {
            var file = Path.Combine(migrationsPath, $"R__{name}.sql");
            var repeatableResult = await Write(file, RepeatableBody(name), cancellationToken);

            return repeatableResult.IsValid ? Results.Success() : repeatableResult;
        }

        var version = migrationVersionProvider.GetNextVersion(migrationsPath, configuration.Versioning);
        var versioned = Path.Combine(migrationsPath, $"V{version}__{name}.sql");
        var versionResult = await Write(versioned, VersionedBody(name), cancellationToken);

        if (!versionResult.IsValid)
            return versionResult;

        if (request.HasUndo)
        {
            var undo = Path.Combine(migrationsPath, $"U{version}__{name}.sql");
            var undoResult = await Write(undo, UndoBody(name), cancellationToken);

            if (!undoResult.IsValid)
                return undoResult;
        }

        return Results.Success();
    }

    private static string NormalizeName(string name)
    {
        var trimmed = name.Trim().ToLowerInvariant();
        var normalized = NameRegex().Replace(trimmed, "_").Trim('_');

        return string.IsNullOrWhiteSpace(normalized) ? throw new InvalidOperationException("Migration name cannot be empty.") : normalized;
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NameRegex();

    private static string ProvisionBody(string name) => $"""
                                                         -- TODO: Add bootstrap migration SQL for {name}.

                                                         """;

    private static string ProvisionUndoBody(string name) => $"""
                                                             -- TODO: Add provision undo SQL for {name}
                                                             -- This should reverse P migration {name} where safely possible.

                                                             """;

    private static string RepeatableBody(string name) => $"""
                                                          -- Repeatable migration: {name}
                                                          -- Use repeatables for idempotent seed/reference data.
                                                          -- This file reruns whenever its checksum changes.

                                                          """;

    private static string UndoBody(string name) => $"""
                                                    -- TODO: Add undo SQL for {name}.
                                                    -- This should reverse V migration {name} where safely possible.

                                                    """;

    private static string VersionedBody(string name) => $"""
                                                         -- TODO: Add forward migration SQL for {name}.

                                                         """;

    private static async Task<Result> Write(string destination, string content, CancellationToken cancellationToken)
    {
        if (File.Exists(destination))
            return Results.Failure($"File already exists: {destination}.");

        await File.WriteAllTextAsync(destination, content, cancellationToken);

        return Results.Success();
    }

    private static async Task<Result> AddProvisionScripts(string migrationsPath, string name, bool hasUndo, CancellationToken cancellationToken)
    {
        var hasProvision = HasFile(migrationsPath, "P__*.sql");
        var hasProvisionUndo = HasFile(migrationsPath, "PU__*.sql");

        if (hasProvision && (!hasUndo || hasProvisionUndo))
            return Results.Failure("Provision script already exists.");

        if (hasUndo && hasProvisionUndo && !hasProvision)
            return Results.Failure("Provision undo script already exists.");

        if (!hasProvision)
        {
            var file = Path.Combine(migrationsPath, $"P__{name}.sql");
            var provisionResult = await Write(file, ProvisionBody(name), cancellationToken);

            if (!provisionResult.IsValid)
                return provisionResult;
        }

        if (hasUndo && !hasProvisionUndo)
        {
            var undo = Path.Combine(migrationsPath, $"PU__{name}.sql");
            var undoResult = await Write(undo, ProvisionUndoBody(name), cancellationToken);

            if (!undoResult.IsValid)
                return undoResult;
        }

        return Results.Success();
    }

    private static bool HasFile(string directory, string pattern) =>
        Directory.Exists(directory) && Directory.EnumerateFiles(directory, pattern, SearchOption.TopDirectoryOnly).Any();
}