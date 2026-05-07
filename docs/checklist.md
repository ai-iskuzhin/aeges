# Aeges MVP Checklist

This checklist tracks implementation slices for the local-first Aeges MVP. It is
more tactical than the roadmap: items should be small enough to complete,
review, test, and document independently.

## Completed

- [x] Create initial .NET 10 solution and project skeleton.
- [x] Add centralized package management.
- [x] Enable nullable, implicit usings, deterministic builds, and warnings as
      errors.
- [x] Add minimal CI workflow for restore, build, and test.
- [x] Add initial architecture, SQLite, task model, runtime layout, and security
      docs.
- [x] Add strongly typed core identifiers.
- [x] Add initial runtime task lifecycle model.
- [x] Validate task lifecycle transitions with unit tests.
- [x] Add XML documentation rule for public contracts.
- [x] Document current public core source APIs with XML comments.
- [x] Define core task iteration model.
- [x] Define core artifact model and artifact types.
- [x] Define core approval model.
- [x] Define core lock model.
- [x] Define core project and machine models.
- [x] Define runner abstraction in `Aeges.Runners`.
- [x] Define runner request model.
- [x] Define runner result model.
- [x] Add storage abstractions in `Aeges.Storage`.
- [x] Define `ITaskRepository`.
- [x] Define `IIterationRepository`.
- [x] Define `IArtifactRepository`.
- [x] Define `IApprovalRepository`.
- [x] Define `IProjectRepository`.
- [x] Define `IMachineRepository`.
- [x] Define `ILockRepository`.
- [x] Define `IUnitOfWork`.
- [x] Ensure all repository APIs accept `CancellationToken`.
- [x] Ensure storage abstractions do not expose SQLite-specific types.
- [x] Add EF Core SQLite package references through central package management.
- [x] Add `AegesDbContext`.
- [x] Add design-time DbContext factory for `dotnet ef` commands.
- [x] Add initial EF Core migration for MVP tables.
- [x] Enable WAL, foreign keys, and busy timeout pragmas.
- [x] Test EF Core can create the schema in a temporary SQLite database.
- [x] Add SQLite project repository implementation.
- [x] Add SQLite machine repository implementation.
- [x] Add SQLite task repository implementation.
- [x] Add SQLite iteration repository implementation.
- [x] Add SQLite artifact repository implementation.
- [x] Add SQLite approval repository implementation.
- [x] Add SQLite lock repository implementation.
- [x] Add SQLite unit-of-work transaction support.
- [x] Add temporary-file SQLite tests for project and machine repositories.
- [x] Add temporary-file SQLite tests for task repository.
- [x] Add temporary-file SQLite tests for iteration, artifact, and approval repositories.
- [x] Add temporary-file SQLite tests for lock repository and transaction rollback.
- [x] Add unit tests for current core lifecycle and value object rules.
- [x] Add project registration application use case.
- [x] Add machine registration and heartbeat application use cases.
- [x] Add task creation and query application use cases.
- [x] Add structured application result model for expected use-case failures.
- [x] Add application tests with in-memory storage fakes.
- [x] Add task status transition application use cases.
- [x] Add task cancellation application use case.
- [x] Add approval request and resolution application use cases.
- [x] Add artifact registration application use cases.
- [x] Add governance policy model.
- [x] Add governance checks before runner dispatch.
- [x] Add lock conflict detection.

## Current Focus

- [ ] Keep `Aeges.Core` infrastructure-free.
- [ ] Add unit tests for future core lifecycle and value object rules.

## Storage Foundation

- [x] Add storage abstractions in `Aeges.Storage`.
- [x] Define `ITaskRepository`.
- [x] Define `IIterationRepository`.
- [x] Define `IArtifactRepository`.
- [x] Define `IApprovalRepository`.
- [x] Define `IProjectRepository`.
- [x] Define `IMachineRepository`.
- [x] Define `ILockRepository`.
- [x] Define `IUnitOfWork`.
- [x] Ensure all repository APIs accept `CancellationToken`.
- [x] Ensure storage abstractions do not expose SQLite-specific types.

## SQLite MVP

- [x] Add EF Core SQLite package references through central package management.
- [x] Add `AegesDbContext`.
- [x] Add design-time DbContext factory for `dotnet ef` commands.
- [x] Add initial EF Core migration for MVP tables.
- [x] Enable WAL, foreign keys, and busy timeout pragmas.
- [x] Add remaining MVP repository implementations.
- [x] Add temporary-file SQLite tests instead of relying only on in-memory
      databases for implemented repositories.
- [x] Test EF Core can create the schema in a temporary SQLite database.
- [x] Test transaction rollback behavior.

## Application Orchestration

- [x] Add project registration use case.
- [x] Add machine registration and heartbeat use cases.
- [x] Add task creation use case.
- [x] Add task query use cases.
- [x] Add task status transition use cases.
- [x] Add task cancellation use case.
- [x] Add approval request and resolution use cases.
- [x] Add artifact registration use cases.
- [x] Add governance checks before runner dispatch.
- [x] Add structured result models for expected runtime failures.

## Runner Contracts

- [ ] Add mock runner for tests.
- [ ] Add Codex runner project contracts without launching Codex yet.
- [ ] Add cancellation and timeout contract coverage.

## Git Foundation

- [ ] Add repository detection contract.
- [ ] Add worktree creation contract.
- [ ] Add git status and diff contracts.
- [ ] Add base commit recording model.
- [ ] Add path safety checks for worktree paths.
- [ ] Test worktree path generation and validation.

## Runtime And CLI

- [ ] Add runtime directory model for `~/.aeges`.
- [ ] Add config loading model.
- [ ] Add `aeges db status`.
- [ ] Add `aeges db migrate`.
- [ ] Add `aeges task create`.
- [ ] Add `aeges task status`.
- [ ] Add `aeges agent run` host shell.
- [ ] Keep CLI usable without Telegram or a control plane.

## Governance

- [x] Add governance policy model.
- [x] Add approval-required decisions for dependency changes.
- [x] Add approval-required decisions for migrations.
- [x] Add approval-required decisions for destructive git operations.
- [x] Add allowed and denied path policy checks.
- [x] Add max iteration and timeout policy checks.
- [x] Add lock conflict detection.

## Documentation

- [ ] Keep public docs aligned with implemented behavior.
- [ ] Update `docs/task-model.md` as task, iteration, approval, and artifact
      models evolve.
- [ ] Update `docs/sqlite.md` when migrations and repository behavior are added.
- [ ] Update `docs/runtime-layout.md` when runtime paths become executable code.
- [ ] Update `docs/security.md` when governance checks are implemented.
- [ ] Update `docs/runners.md` when runner implementations are added.
- [ ] Update `docs/storage.md` when repository implementations are added.

## Definition Of Done

- [ ] Changes are scoped to the relevant project boundary.
- [ ] Public contracts have XML documentation comments.
- [ ] Expected failures use explicit result models where practical.
- [ ] Long-running APIs accept `CancellationToken`.
- [ ] Tests cover the behavior added or changed.
- [ ] `dotnet restore` passes.
- [ ] `dotnet build` passes with zero warnings.
- [ ] `dotnet test` passes.
- [ ] Documentation is updated when behavior changes.
