# Storage

`Aeges.Storage` defines persistence abstractions for the application layer. It
depends on `Aeges.Core` and must not expose EF Core, SQLite, or provider-specific
types.

## Repositories

The initial storage contracts include:

- `ITaskRepository`
- `IIterationRepository`
- `IArtifactRepository`
- `IApprovalRepository`
- `IProjectRepository`
- `IMachineRepository`
- `ILockRepository`

Repository methods are asynchronous and accept `CancellationToken`.

## Unit Of Work

`IUnitOfWork` groups repositories and provides:

- `SaveChangesAsync`
- transaction execution helpers

The SQLite implementation may use EF Core internally, but that detail belongs in
`Aeges.Storage.Sqlite`.

## SQLite Implementation

`Aeges.Storage.Sqlite` now owns the EF Core context, persistence records, entity
configuration, design-time factory, and migrations. Repository implementations
will adapt between the provider-neutral storage contracts and these internal EF
records.

Implemented SQLite repositories:

- `SqliteProjectRepository`
- `SqliteMachineRepository`
- `SqliteTaskRepository`
- `SqliteIterationRepository`
- `SqliteArtifactRepository`
- `SqliteApprovalRepository`
- `SqliteLockRepository`
- `SqliteUnitOfWork`

SQLite repositories follow the same adapter pattern: map EF records to core
domain models through explicit domain rehydration APIs, keep EF types out of
storage abstractions, and test against temporary SQLite database files.
