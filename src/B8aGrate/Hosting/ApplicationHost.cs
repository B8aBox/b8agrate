using B8aGrate.Application.Features.AdoptExistingMigrations;
using B8aGrate.Application.Features.ApplyMigrations;
using B8aGrate.Application.Features.CreateBaseline;
using B8aGrate.Application.Features.CreateMigrationScript;
using B8aGrate.Application.Features.GetMigrationInformation;
using B8aGrate.Application.Features.GetMigrationSnapshot;
using B8aGrate.Application.Features.InitializeProject;
using B8aGrate.Application.Features.RepairMigrations;
using B8aGrate.Application.Features.UndoMigrations;
using B8aGrate.Application.Features.UndoProvisioning;
using B8aGrate.Application.Features.ValidateMigrationHistory;
using B8aGrate.Hosting.Interfaces;
using B8aGrate.Infrastructure;
using B8aGrate.Rendering;
using MediatR;
using YuckQi.Domain.Validation.Abstract.Interfaces;
using YuckQi.Extensions.Mapping.Abstractions.Abstract.Interfaces;

namespace B8aGrate.Hosting;

public sealed class ApplicationHost(IMediator mediator, IMapper mapper, ResultRenderer resultRenderer) : IApplicationHost
{
    #region Public Methods

    public async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0 || args.Contains("--help", StringComparer.OrdinalIgnoreCase) || args.Contains("-h", StringComparer.OrdinalIgnoreCase))
        {
            PrintHelp();

            return 0;
        }

        var command = args[0].ToLowerInvariant();
        var options = Args.Parse(args.Skip(1).ToArray());
        var output = options.Get("output") ?? "text";
        var root = Directory.GetCurrentDirectory();
        var source = Tuple.Create(options.Values, root);
        var request = GetRequest(command, source);
        var result = await mediator.Send(request) as IResult ?? throw new InvalidOperationException($"Command '{command}' returned an invalid result.");

        return resultRenderer.Render(result, output);
    }

    #endregion


    #region Private Methods

    private object GetRequest(string command, Tuple<Dictionary<string, string?>, string> source) => command switch
    {
        "add" => mapper.Map<CreateMigrationScriptCommand>(source),
        "adopt" => mapper.Map<AdoptExistingMigrationsCommand>(source),
        "baseline" => mapper.Map<CreateBaselineCommand>(source),
        "info" => mapper.Map<GetMigrationInformationQuery>(source),
        "init" => mapper.Map<InitializeProjectCommand>(source),
        "migrate" => mapper.Map<ApplyMigrationsCommand>(source),
        "repair" => mapper.Map<RepairMigrationsCommand>(source),
        "snapshot" => mapper.Map<GetMigrationSnapshotQuery>(source),
        "undo" => mapper.Map<UndoMigrationsCommand>(source),
        "unprovision" => mapper.Map<UndoProvisioningCommand>(source),
        "validate" => mapper.Map<ValidateMigrationHistoryCommand>(source),
        _ => throw new InvalidOperationException($"Unknown command: {command}.")
    };

    private static void PrintHelp()
    {
        Console.WriteLine("""
                          B8aGrate - lightweight SQL migrations for SQL Server and PostgreSQL

                          Commands:
                            init        Create provider-specific migration structure, config, and sample files
                            add         Create a new V/U migration pair or repeatable R migration
                            adopt       Mark existing V scripts as already applied without executing them
                            baseline    Mark an existing database as migrated up to a version
                            info        Show migration history and pending scripts
                            migrate     Run pending V scripts and changed R scripts
                            repair      Remove failed rows and/or update stored checksums
                            snapshot    Show current migration state for audits and pipelines
                            undo        Undo applied versioned migrations using U scripts
                            unprovision Undo applied provisioning scripts using PU scripts
                            validate    Validate checksums for applied V/R scripts

                          Options:
                            --connection "connection string"    Optional when B8AGRATE_CONNECTION is set
                            --admin-connection "connection"     Optional when B8AGRATE_ADMIN_CONNECTION is set
                            --output text|json

                          Init options:
                            --provider sqlserver|postgres
                            --path ./data/migrations            Sets migration.path in b8agrate.json
                            --full                              Creates migrations, templates, and readme
                            --with-readme                       Creates a sample README.md file
                            --with-templates                    Creates samples template files
                            --project ./src/MyApi/MyApi.csproj  Adds optional MSBuild publish target

                          Add options:
                            b8agrate add --name create_users
                            b8agrate add --name seed_countries --repeatable
                            --name create_users, -n create_users
                            --provision, -p                     Create P__name.sql and PU__name.sql bootstrap scripts
                            --repeatable, -r                    Create R__name.sql instead of V/U files
                            --no-undo                           Create only V file

                          Migration options:
                            --target 2026061501                 Used by undo and adopt.
                                                                  Undo rolls back down to target exclusive.
                                                                  Adopt marks V scripts up to target as applied.
                            --steps 1                           Used by undo. Rolls back the latest N applied versions.
                            --version 2026061501                Used by baseline.
                            --description "existing db"         Used by baseline.
                            --dry-run                           Print what would run without executing scripts. Supported by migrate, undo, unprovision, adopt, and repair.
                            --remove-failed                     Used by repair. Deletes failed history rows.
                            --update-checksums                  Used by repair. Replaces stored checksums with local file checksums.
                            --all                               Used by adopt. Adopt all discovered V scripts.

                          Config:
                            init creates b8agrate.json in the current directory.
                            Commands read provider, migration path, schema, table, timeout, versioning, and environment variable names from config.
                            Connection strings should be passed with --connection or the B8AGRATE_CONNECTION environment variable.

                          Naming:
                            V000001__create_users.sql            Default Sequential strategy
                            U000001__drop_users.sql
                            P__create_database.sql               Provisioning script, runs before V/R with admin connection
                            PU__drop_database.sql                Provision undo script, runs through unprovision
                            V20260616143015__create_users.sql    Timestamp strategy
                            V2026061601__create_users.sql        DateSequence strategy
                            R__seed_countries.sql

                          Examples:
                            b8agrate init --provider sqlserver --path ./data/migrations --full
                            b8agrate add --name create_users
                            b8agrate add --name seed_countries --repeatable
                            b8agrate migrate --connection "$DATABASE_URL"
                            b8agrate baseline --connection "..." --version 2026061501
                            b8agrate repair --connection "..." --update-checksums --remove-failed
                            b8agrate adopt --connection "..." --all --dry-run
                            b8agrate adopt --connection "..." --target 2026061501
                            b8agrate unprovision --admin-connection "..."
                            b8agrate snapshot --connection "..." --output json
                          """);
    }

    #endregion
}