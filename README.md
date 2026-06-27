# b8agrate

**b8agrate** is a lightweight .NET SQL migration CLI for SQL Server and PostgreSQL.

The name is a play on **b8abox** + **migrate**: a database migration tool inside the box.

It uses Flyway-style script names, but includes free undo support:

```text
V000001__create_users.sql     # versioned, runs once
U000001__drop_users.sql       # undo for matching V version
P__create_database.sql        # provisioning, runs before versioned/repeatable scripts
PU__drop_database.sql         # provision undo, run explicitly with unprovision
R__seed_countries.sql         # repeatable, reruns idempotent reference data when checksum changes
```

## Install locally

Pack the tool, then install from the local package folder:

```bash
dotnet pack ./src/B8aGrate/B8aGrate.csproj -c Release
dotnet tool install --global B8aGrate \
  --add-source ./src/B8aGrate/bin/Release \
  --ignore-failed-sources
```

`--add-source` adds the local package folder, but NuGet still checks configured package sources. `--ignore-failed-sources` prevents an unavailable private feed from failing the local install.

If the tool is already installed, update it instead:

```bash
dotnet tool update --global B8aGrate \
  --add-source ./src/B8aGrate/bin/Release \
  --ignore-failed-sources
```

## Quick start

```bash
b8agrate init --provider sqlserver --path ./db/migrations --full
b8agrate add --name create_users
b8agrate add --name seed_countries --repeatable
b8agrate migrate --connection "..."
```

`init` creates a `b8agrate.json` file, so later commands can read provider/schema/table defaults from config. The connection string can be passed with `--connection` or with the `B8AGRATE_CONNECTION` environment variable.

## Init

```bash
b8agrate init --provider sqlserver --path ./db/migrations
```

Creates:

```text
db/
└── migrations/
    └── b8agrate.json
```

Use `--full` to also create sibling folders for seeds and reference data:

```bash
b8agrate init --provider postgres --path ./database/migrations --full
```

Creates:

```text
database/
├── migrations/
    ├── b8agrate.json
    ├── README.md
    ├── VyyyyMMddHHmmss__initial_schema.sql
    ├── UyyyyMMddHHmmss__drop_initial_schema.sql
    └── R__seed_countries.sql
```

You can also add an optional MSBuild publish target to a project file:

```bash
b8agrate init \
  --provider sqlserver \
  --path ./db/migrations \
  --project ./src/MyApi/MyApi.csproj
```

The injected target is disabled by default and only runs when `RunB8aGrateMigrations=true` is passed.

## Config file

`init` creates this file in the root folder:

```json
{
  "environmentVariables": {
    "connection": "B8AGRATE_CONNECTION",
    "adminConnection": "B8AGRATE_ADMIN_CONNECTION"
  },
  "migration": {
    "schema": "b8agrate",
    "table": "Migration",
  },
  "provider": "SqlServer"
}
```

After that, you can run:

```bash
export B8AGRATE_CONNECTION="Server=..."
export B8AGRATE_ADMIN_CONNECTION="Server=...;Database=master;..."
b8agrate migrate
```

instead of passing provider/schema/table/connection every time.

## Add migrations

Create a versioned migration and matching undo script:

```bash
b8agrate add --name create_users
```

Creates:

```text
VyyyyMMddHHmmss__create_users.sql
UyyyyMMddHHmmss__create_users.sql
```

Create only a forward migration:

```bash
b8agrate add --name create_users --no-undo
```

Create a repeatable migration:

```bash
b8agrate add --name seed_countries --repeatable
```

Creates:

```text
R__seed_countries.sql
```

## Core commands

```bash
b8agrate migrate --connection "..."
b8agrate info --connection "..."
b8agrate validate --connection "..."
b8agrate undo --connection "..." --steps 1
b8agrate undo --connection "..." --target 000001
b8agrate unprovision --connection "..." --admin-connection "..."
b8agrate baseline --connection "..." --version 000001 --description "existing production database"
b8agrate repair --connection "..." --remove-failed --update-checksums
b8agrate adopt --connection "..." --all --dry-run
b8agrate adopt --connection "..." --up-to 000001 --validate
b8agrate snapshot --connection "..." --output json
```

Use `--provider postgres` or `--provider sqlserver` to override the config value.

Default history location:

```text
schema: b8agrate
table:  Migration
```

Override with:

```bash
--schema custom_schema --table custom_history_table
```

## JSON output for pipelines

Every command supports `--output json`:

```bash
b8agrate migrate --connection "$DATABASE_URL" --output json
```

Example shape:

```json
{
  "command": "migrate",
  "provider": "PostgreSql",
  "success": true,
  "applied": [
    {
      "version": "000001",
      "type": "Versioned",
      "script": "V000001__create_app_user.sql",
      "checksum": "...",
      "executionMs": 142,
      "dryRun": false
    }
  ],
  "skipped": [],
  "issues": [],
  "messages": []
}
```

## Production-grade features included

### Baseline

`baseline` marks an existing database as already migrated up to a given version. This is useful when adopting b8agrate on a database that already exists.

```bash
b8agrate baseline --connection "$DATABASE_URL" --version 000001
```

This inserts a `Baseline` row into the history table. Future `migrate` runs skip `V` scripts less than or equal to the baseline version.

### Repair

`repair` is for fixing history metadata only. It does **not** run migration SQL.

```bash
b8agrate repair --connection "$DATABASE_URL" --remove-failed
b8agrate repair --connection "$DATABASE_URL" --update-checksums
```

- `--remove-failed` deletes failed rows from the history table.
- `--update-checksums` updates stored checksums to match local files.


### Adopt existing migrations

`adopt` is for bringing an existing database into b8agrate without rerunning scripts that were already applied manually or by another tool.

Unlike `baseline`, which inserts one baseline marker, `adopt` inserts one history row per discovered `V` script. That preserves script-level history and checksums.

Preview adoption without writing history rows:

```bash
b8agrate adopt --connection "$DATABASE_URL" --all --dry-run
```

Adopt every discovered versioned migration:

```bash
b8agrate adopt --connection "$DATABASE_URL" --all
```

Adopt only scripts up to a version:

```bash
b8agrate adopt --connection "$DATABASE_URL" --up-to 000001
```

Validate before adoption:

```bash
b8agrate adopt --connection "$DATABASE_URL" --all --validate
```

Validation checks duplicate local versions and conflicts with existing history rows. It does not prove that every SQL object exists in the database; use `--force` only when you know the database already matches those scripts.

Adopted rows are stored with kind `Adopted`, so audit history can distinguish inherited migrations from scripts actually executed by b8agrate. `migrate`, `info`, `validate`, and `undo` treat adopted versioned migrations as effectively applied.

### Snapshot

`snapshot` returns the current migration state for audits and CI/CD.

```bash
b8agrate snapshot --connection "$DATABASE_URL"
b8agrate snapshot --connection "$DATABASE_URL" --output json
```

It reports applied/adopted/baseline/undo rows and pending versioned scripts.

### Provider-specific init templates

`init` creates provider-specific scaffolding from embedded SQL templates.

SQL Server uses syntax like:

```sql
CHAR(2)
NVARCHAR(100)
DATETIME2
SYSUTCDATETIME()
GO
```

PostgreSQL uses syntax like:

```sql
CHAR(2)
VARCHAR(100)
TIMESTAMPTZ
now()
ON CONFLICT (code) DO NOTHING
```

### Undo behavior

Undo scripts are recorded as `Undo` rows. Original `Versioned` rows remain for audit history. The engine calculates the effective applied state by replaying history in order: `Versioned` adds a version; `Undo` removes it.

By default:

```bash
b8agrate undo --connection "..."
```

undoes the latest effective versioned migration.

Undo a fixed number of versions:

```bash
b8agrate undo --connection "..." --steps 2
```

Undo down to a target version, exclusive:

```bash
b8agrate undo --connection "..." --target 000001
```

### Provisioning

Provisioning scripts run before normal `V` and `R` migrations and use the admin connection when one is configured:

```text
P__create_database_and_roles.sql
PU__drop_database_and_roles.sql
```

Use provisioning for database-level setup such as creating databases, logins, roles, users, grants, or provider extensions. Pass the admin connection with `--admin-connection` or `B8AGRATE_ADMIN_CONNECTION`; if omitted, b8agrate uses the normal connection.

```bash
b8agrate migrate \
  --connection "$B8AGRATE_CONNECTION" \
  --admin-connection "$B8AGRATE_ADMIN_CONNECTION"
```

Only one `P__*.sql` script is allowed. It is bootstrap-only: use it for initial database/user setup before versioned migrations exist. After versioned, adopted, or baseline history exists, new provisioning scripts are rejected. The migration user should handle later target-database changes through `V` scripts.

Provisioning scripts are recorded as `Provision` rows in the same history table. If the target database does not exist yet, b8agrate runs the `P` script first and records it after the normal connection can be opened. Keep the `P` script idempotent so retrying after a partial failure is safe.

Provision undo scripts are intentionally explicit:

```bash
b8agrate unprovision --connection "$B8AGRATE_CONNECTION" --admin-connection "$B8AGRATE_ADMIN_CONNECTION"
```

`unprovision` reads the normal history table first, so it needs both the target connection and the admin connection:

```bash
b8agrate unprovision \
  --connection "$B8AGRATE_CONNECTION" \
  --admin-connection "$B8AGRATE_ADMIN_CONNECTION"
```

`PU` scripts are recorded as `ProvisionUndo` rows when the target database is still reachable after execution.

### Repeatables

Repeatable scripts rerun when their checksum changes. They do not have undo scripts. Keep repeatables limited to idempotent seed/reference-data synchronization. Use versioned migrations for schema objects such as tables, views, functions, and stored procedures.

## SQL Server notes

- Uses `sp_getapplock` for migration locking.
- Splits scripts on `GO`.
- Uses `GO` batch separators in generated SQL Server templates.

## PostgreSQL notes

- Uses `pg_advisory_lock` for migration locking.
- Runs each script as one command by default.
- Generated templates use `gen_random_uuid()`, which may require `pgcrypto` depending on your PostgreSQL version/configuration.

## Transaction opt-out

By default, scripts run in a transaction. Add this directive in the first 20 lines to opt out:

```sql
-- migrate:transaction=false
```

## Build / install locally

```bash
dotnet build ./src/B8aGrate/B8aGrate.csproj

dotnet pack ./src/B8aGrate/B8aGrate.csproj -c Release

dotnet tool install --global --add-source ./src/B8aGrate/bin/Release B8aGrate
```

After installing:

```bash
b8agrate --help
```

## Rider / Visual Studio

Open `b8agrate.sln`, set the CLI project as startup, and pass arguments in the run configuration.

For local dev you can use the optional MSBuild target generated by `init --project`, but avoid automatically running migrations on every build outside dev environments.

Example:

```bash
dotnet publish ./src/MyApi/MyApi.csproj
```

## Release pipeline example

```bash
dotnet tool restore
export B8AGRATE_CONNECTION="$DATABASE_URL"
b8agrate validate --output json
b8agrate migrate --output json
```

## Production hardening now included

- Testcontainers integration test project for SQL Server and PostgreSQL.
- `repair --dry-run` to preview failed-row removal and checksum repair.
- `adopt --validate` object checks for common `CREATE TABLE`, `CREATE VIEW`, `CREATE OR ALTER VIEW`, and PostgreSQL `CREATE OR REPLACE VIEW` patterns.
- Configurable command timeout through `commandTimeoutSeconds` or `--command-timeout`.
- Script placeholder replacement from `b8agrate.json`, for example `${schema}`.


## Command timeout

Default command timeout is 30 seconds:

```json
{
  "commandTimeoutSeconds": 60
}
```

You can override it per command:

```bash
b8agrate migrate --command-timeout 120 --connection "$B8AGRATE_CONNECTION"
```

## Repair dry run

Preview repair actions without updating the history table:

```bash
b8agrate repair --remove-failed --update-checksums --dry-run --connection "$B8AGRATE_CONNECTION"
```

## Integration tests

The `tests/B8aGrate.Tests` project includes Testcontainers-based SQL Server and PostgreSQL tests. Docker must be running locally:

```bash
dotnet test
```

## Remaining next hardening steps

- Add GitHub Action and Azure DevOps task wrappers.
- Add signed NuGet package publishing.
- Add a lock timeout setting separate from command timeout.
- Add richer SQL parsing for stored procedures, functions, indexes, triggers, and quoted edge cases.
- Add provider-specific idempotent helper snippets for common scaffolded migrations.

## Versioning strategies

`b8agrate` now defaults to sequential versions:

```text
V000001__create_users.sql
U000001__create_users.sql
V000002__create_groups.sql
U000002__create_groups.sql
```

The generated `b8agrate.json` includes:

```json
{
  "command": {
    "timeoutSeconds": 30
  },
  "migration: {
    "schema": "b8agrate",
    "table": "Migration",
  },
  "provider": "SqlServer",
  "versioning": {
    "strategy": "Sequential",
    "sequentialWidth": 6
  }
}
```

Supported generated formats:

```text
Sequential:   V000001__create_users.sql
Timestamp:    V20260616143015__create_users.sql
DateSequence: V2026061601__create_users.sql
Repeatable:   R__seed_countries.sql
```

Versions are stored as strings in the history table so legacy date-based scripts, timestamp scripts, sequential scripts, and semantic versions can all be parsed.
