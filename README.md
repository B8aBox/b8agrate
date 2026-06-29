<h1>
  <img src="https://raw.githubusercontent.com/B8aBox/b8agrate/main/assets/icon.png" alt="b8agrate logo" width="40" />
  b8agrate
</h1>

**b8agrate** is a lightweight .NET SQL migration CLI for SQL Server and PostgreSQL.

It uses Flyway-style script names, adds explicit undo scripts, and stores migration history in the target database:

```text
P__bootstrap.sql              # provisioning script, runs before normal migrations
PU__undo_bootstrap.sql        # provisioning undo script, run explicitly with unprovision
V000001__create_users.sql     # versioned migration, runs once
U000001__create_users.sql     # undo for the matching V version
R__seed_countries.sql         # repeatable migration, reruns when checksum changes
```

## Requirements

- .NET 10 SDK for building and packing the tool.
- SQL Server or PostgreSQL for running migrations.
- Docker, only when running the Testcontainers integration tests.

## Install locally

Pack the tool, then install it from the local package folder:

```bash
dotnet pack ./src/B8aGrate/B8aGrate.csproj -c Release
dotnet tool install --global B8aGrate \
  --add-source ./src/B8aGrate/bin/Release \
  --ignore-failed-sources
```

If the tool is already installed, update it instead:

```bash
dotnet tool update --global B8aGrate \
  --add-source ./src/B8aGrate/bin/Release \
  --ignore-failed-sources
```

After installing:

```bash
b8agrate --help
```

## Quick start

```bash
b8agrate init
b8agrate add --name create_users
b8agrate add --name seed_countries --repeatable
export B8AGRATE_CONNECTION="Server=..."
b8agrate migrate
```

`init` creates `b8agrate.json` in the current directory. Commands read provider, migration path, schema, table, timeout, versioning, and environment variable names from that file.

Connection strings can be passed with `--connection` or read from the configured environment variable. By default, that variable is `B8AGRATE_CONNECTION`.

## Configuration

The generated `b8agrate.json` looks like this:

```json
{
  "command": {
    "timeout": 30
  },
  "environmentVariables": {
    "adminConnection": "B8AGRATE_ADMIN_CONNECTION",
    "connection": "B8AGRATE_CONNECTION"
  },
  "migration": {
    "path": "./data/migrations",
    "schema": "b8agrate",
    "table": "Migration"
  },
  "provider": "SqlServer",
  "versioning": {
    "strategy": "Sequential"
  }
}
```

Important details:

- `migration.path` is resolved relative to the directory where you run `b8agrate`.
- `migration.schema` and `migration.table` control the history table location.
- `command.timeout` sets the database command timeout in seconds.
- `provider` is `SqlServer` or `PostgreSql`.
- `environmentVariables.connection` and `environmentVariables.adminConnection` name the env vars used when CLI connection flags are omitted.

## Initialize a project

```bash
b8agrate init --provider sqlserver --path ./data/migrations
```

Creates `b8agrate.json` in the current directory and creates the configured migrations folder.

Use `--full` to also create sample SQL templates and a migrations README:

```bash
b8agrate init --provider postgres --path ./data/migrations --full
```

With `--full`, the migration folder includes files like:

```text
data/migrations/
├── README.md
├── P__bootstrap.sql
├── PU__undo_bootstrap.sql
├── V000001__initial_schema.sql
├── U000001__drop_initial_schema.sql
└── R__seed_countries.sql
```

You can request only part of that scaffolding:

```bash
b8agrate init --provider sqlserver --path ./data/migrations --with-readme
b8agrate init --provider sqlserver --path ./data/migrations --with-templates
```

You can also add a simple MSBuild target to a project file:

```bash
b8agrate init \
  --provider sqlserver \
  --path ./data/migrations \
  --project ./src/MyApi/MyApi.csproj
```

The injected target runs `b8agrate migrate` after Debug builds.

## Add migration scripts

Create a versioned migration and matching undo script:

```bash
b8agrate add --name create_users
```

Creates:

```text
V000001__create_users.sql
U000001__create_users.sql
```

Create only the forward versioned migration:

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

Create provisioning scripts:

```bash
b8agrate add --name bootstrap --provision
```

Creates:

```text
P__bootstrap.sql
PU__bootstrap.sql
```

Only one `P__*.sql` and one `PU__*.sql` file are allowed in a migration folder.

## Versioning

The default generated versioning strategy is sequential:

```text
V000001__create_users.sql
U000001__create_users.sql
V000002__create_groups.sql
U000002__create_groups.sql
```

Supported generated strategies:

```text
Sequential:   V000001__create_users.sql
Timestamp:    V20260616143015__create_users.sql
DateSequence: V2026061601__create_users.sql
Repeatable:   R__seed_countries.sql
```

Configure strategy in `b8agrate.json`:

```json
{
  "versioning": {
    "strategy": "Sequential",
    "sequentialWidth": 6
  }
}
```

Versions are stored as strings, and discovered `V`/`U` versions may contain letters, numbers, underscores, periods, and dashes.

## Commands

Run pending versioned migrations and changed repeatables:

```bash
b8agrate migrate --connection "$DATABASE_URL"
b8agrate migrate --dry-run --connection "$DATABASE_URL"
```

Show history and pending scripts:

```bash
b8agrate info --connection "$DATABASE_URL"
```

Validate applied checksums against local scripts:

```bash
b8agrate validate --connection "$DATABASE_URL"
```

Undo the latest effective versioned migration:

```bash
b8agrate undo --connection "$DATABASE_URL"
```

Undo a fixed number of versions:

```bash
b8agrate undo --steps 2 --connection "$DATABASE_URL"
```

Undo down to a target version, exclusive:

```bash
b8agrate undo --target 000001 --connection "$DATABASE_URL"
```

Mark an existing database as migrated up to a version:

```bash
b8agrate baseline --version 000001 --description "existing production database" --connection "$DATABASE_URL"
```

Adopt existing versioned scripts without executing them:

```bash
b8agrate adopt --all --connection "$DATABASE_URL"
b8agrate adopt --target 000003 --connection "$DATABASE_URL"
b8agrate adopt --all --dry-run --connection "$DATABASE_URL"
```

Repair history metadata:

```bash
b8agrate repair --remove-failed --connection "$DATABASE_URL"
b8agrate repair --update-checksums --connection "$DATABASE_URL"
b8agrate repair --remove-failed --update-checksums --dry-run --connection "$DATABASE_URL"
```

Return the current migration state for audits or pipelines:

```bash
b8agrate snapshot --connection "$DATABASE_URL"
```

Undo provisioning:

```bash
b8agrate unprovision \
  --connection "$B8AGRATE_CONNECTION" \
  --admin-connection "$B8AGRATE_ADMIN_CONNECTION"
```

Most database commands support `--connection` and `-c`. Commands that run provisioning also support `--admin-connection`.

## JSON output

Commands can render JSON with `--output json`:

```bash
b8agrate migrate --connection "$DATABASE_URL" --output json
b8agrate snapshot --connection "$DATABASE_URL" --output json
```

This is intended for CI/CD pipelines and audit scripts.

## Baseline and adopt

`baseline` inserts a single `Baseline` history row. Future `migrate` runs skip `V` scripts less than or equal to that version.

`adopt` inserts one `Adopted` row per discovered versioned script. This is useful when the database already matches local migration scripts and you want b8agrate history to preserve script-level versions and checksums.

`adopt --dry-run` reports adoption candidates without writing rows. In dry-run mode, discovery and history conflicts are reported in the result instead of failing the command.

## Undo behavior

Undo scripts are recorded as `Undo` rows. Original `Versioned` or `Adopted` rows remain for audit history.

The engine calculates the effective applied state by replaying history in order:

- `Versioned` and `Adopted` add a version.
- `Undo` removes that version from the effective state.
- `Baseline` covers versioned scripts less than or equal to the baseline version.

## Provisioning

Provisioning scripts run before normal `V` and `R` migrations and use the admin connection when one is configured:

```text
P__create_database_and_roles.sql
PU__drop_database_and_roles.sql
```

Use provisioning for bootstrap database-level setup such as creating databases, logins, roles, users, grants, or provider extensions.

Pass the admin connection with `--admin-connection` or the configured admin environment variable. By default, that variable is `B8AGRATE_ADMIN_CONNECTION`. If no admin connection is configured, b8agrate uses the normal connection.

Provisioning is bootstrap-only:

- Only one `P__*.sql` script is allowed.
- If the target database cannot be opened, b8agrate runs `P` through the admin connection, then opens the target connection and records the row.
- If the target database is already reachable and `P` has not been recorded, `migrate` rejects the pending provision script.
- Keep `P` scripts idempotent so retrying after a partial failure is safe.

Provision undo scripts are intentionally explicit and run through `unprovision`.

## Repeatables

Repeatable scripts rerun when their checksum changes. They do not have undo scripts.

Use repeatables for idempotent seed or reference-data synchronization. Use versioned migrations for schema changes.

## Transaction behavior

Normal versioned, undo, and repeatable scripts run in a transaction by default.

Add this directive in the first 20 lines to opt out for a script:

```sql
-- migrate:transaction=false
```

Provisioning and provision-undo scripts run without wrapping the script in a migration transaction.

## SQL Server notes

- Uses `sp_getapplock` for migration locking.
- Splits scripts on `GO`.
- Generated SQL Server templates use `GO` batch separators.

## PostgreSQL notes

- Uses `pg_advisory_lock` for migration locking.
- Runs each script as one command by default.
- Generated templates use PostgreSQL-specific syntax such as `TIMESTAMPTZ`, `now()`, and `ON CONFLICT`.

## Build and test

```bash
dotnet build ./src/B8aGrate/B8aGrate.csproj
dotnet test
```

The `tests/B8aGrate.Tests` project includes SQL Server and PostgreSQL integration tests using Testcontainers. Docker must be running locally for those tests.

## Rider / Visual Studio

Open `b8agrate.sln`, set the CLI project as the startup project, and pass command arguments in the run configuration.

For local development, you can use the optional MSBuild target generated by `init --project`, but be deliberate about running migrations automatically as part of builds.

## Release pipeline example

```bash
export B8AGRATE_CONNECTION="$DATABASE_URL"
b8agrate validate --output json
b8agrate migrate --output json
b8agrate snapshot --output json
```
