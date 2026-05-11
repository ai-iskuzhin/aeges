# SQLite Storage

SQLite is the default local persistence engine for the MVP.

Use EF Core SQLite for the persistence implementation. Keep EF Core details
inside `Aeges.Storage.Sqlite`; storage abstractions must not expose EF-specific
types.

It stores durable runtime state and metadata:

- projects
- tasks
- task iterations
- artifact metadata
- approval requests
- locks
- runtime events
- runner executions
- machines

Large artifact contents should live on disk. SQLite stores artifact identifiers,
relative paths, size, checksum, type, and timestamps.

## Runtime Database

Default database path:

```text
~/.aeges/aeges.db
```

Recommended pragmas:

```sql
PRAGMA journal_mode = WAL;
PRAGMA foreign_keys = ON;
PRAGMA busy_timeout = 5000;
```

Schema definitions and migrations belong to `Aeges.Storage.Sqlite`.

The current EF Core foundation includes:

- `AegesDbContext`
- internal persistence records for MVP tables
- explicit entity configuration
- `AegesDbContextFactory` for `dotnet ef`
- an initial `InitialCreate` migration
- `AddProjectArchiveState` for non-destructive project archiving
- SQLite pragma application for WAL, foreign keys, and busy timeout
- SQLite project, machine, task, iteration, artifact, approval, and lock
  repositories backed by temporary-file tests
- SQLite runner execution repository for durable worker process launch records
- SQLite unit-of-work transaction tests for commit and rollback behavior

## Migrations

Use EF Core migrations rather than a custom SQL migration runner.

Migration files should live under:

```text
src/Aeges.Storage.Sqlite/Migrations/
```

Create migrations with:

```text
dotnet ef migrations add <Name> --project src/Aeges.Storage.Sqlite --startup-project src/Aeges.Cli
```

Apply migrations with:

```text
dotnet ef database update --project src/Aeges.Storage.Sqlite --startup-project src/Aeges.Cli
```

The CLI also exposes:

```text
aeges db status
aeges db migrate
aeges project add
aeges project list
aeges machine add
aeges machine list
aeges task create
aeges task status
aeges agent run
```

These commands use the configured SQLite connection string when present, or the
default runtime database at `~/.aeges/aeges.db` otherwise. `db status` reports
applied and pending EF Core migrations. `db migrate` applies pending migrations
and then reports the resulting status.

Task commands initialize the local SQLite database before use, then delegate to
the application task service. Creating a task requires an existing project and
machine because the runtime stores tasks as durable work assigned inside known
execution boundaries.

Project and machine commands provide the local-only setup path for the task
commands. A developer can migrate the database, add a project, add a machine,
and create a queued task without Telegram or a future control plane.

`agent run --once` records a heartbeat and may claim one queued task assigned to
the configured machine. The claim moves the task into planning and creates a
bounded iteration with the configured runner id. Use `--no-claim` to keep the
command as a heartbeat and queue-preview check only.

Runtime code may call EF Core migration APIs during local agent startup when
configured to manage the local SQLite database automatically.
