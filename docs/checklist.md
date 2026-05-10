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
- [x] Define `IRunnerExecutionRepository`.
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
- [x] Add SQLite runner execution repository implementation.
- [x] Add SQLite unit-of-work transaction support.
- [x] Add temporary-file SQLite tests for project and machine repositories.
- [x] Add temporary-file SQLite tests for task repository.
- [x] Add temporary-file SQLite tests for iteration, artifact, and approval repositories.
- [x] Add temporary-file SQLite tests for lock repository and transaction rollback.
- [x] Add temporary-file SQLite tests for runner execution repository.
- [x] Add unit tests for current core lifecycle and value object rules.
- [x] Add project registration application use case.
- [x] Add machine registration and heartbeat application use cases.
- [x] Add task creation and query application use cases.
- [x] Add bounded task iteration creation application use case.
- [x] Add structured application result model for expected use-case failures.
- [x] Add application tests with in-memory storage fakes.
- [x] Add task status transition application use cases.
- [x] Add task cancellation application use case.
- [x] Add approval request and resolution application use cases.
- [x] Add artifact registration application use cases.
- [x] Add governance policy model.
- [x] Add governance checks before runner dispatch.
- [x] Add application use case for durable runner execution tracking.
- [x] Add deterministic runner request creation from task, iteration, project,
      and runtime layout state.
- [x] Add lock conflict detection.
- [x] Add mock runner for tests.
- [x] Add Codex runner project contracts without launching Codex yet.
- [x] Add Codex CLI runner process shell.
- [x] Add cancellation and timeout contract coverage.
- [x] Add Git repository, worktree, status, diff, and base commit contracts.
- [x] Add worktree path generation and validation.
- [x] Add runtime directory model for `~/.aeges`.
- [x] Add config loading model.
- [x] Add `aeges db status`.
- [x] Add `aeges db migrate`.
- [x] Add `aeges task create`.
- [x] Add `aeges task status`.
- [x] Add `aeges agent run` host shell.
- [x] Make local agent claim one queued task and create its first bounded iteration.
- [x] Make local agent prepare prompt artifact metadata and deterministic runner
      paths for claimed iterations.
- [x] Add opt-in local agent runner execution with durable runner execution
      records.
- [x] Add `aeges agent start/status/stop` process wrapper for background queue
      processing.
- [x] Keep CLI usable without Telegram or a control plane.
- [x] Add button-first Telegram interaction handler.
- [x] Add Telegram application facade boundary.
- [x] Add Telegram long-polling transport service.
- [x] Add CLI host command for Telegram long polling.
- [x] Add Telegram approval list and resolution buttons.
- [x] Add CLI and Telegram task cancellation.
- [x] Add first-user README and safe sample local configuration.
- [x] Add interactive Telegram token setup for CLI runs.
- [x] Add Telegram setup wizard with local secret-file support.
- [x] Add Telegram token/config check command.
- [x] Simplify Telegram setup to default to local secret files.
- [x] Package CLI as a local .NET tool named `aeges`.
- [x] Add install roadmap.
- [x] Add shell installer script for .NET tool based local installs.
- [x] Add GitHub release workflow for installer, package, and checksum assets.
- [x] Add Windows PowerShell installer for local and release package installs.

## Current Focus

- [ ] Keep `Aeges.Core` infrastructure-free.
- [ ] Add unit tests for future core lifecycle and value object rules.
- [ ] Publish first version-tagged release and test remote installer URL.
- [ ] Add Homebrew formula after release artifacts are stable.

## Storage Foundation

- [x] Add storage abstractions in `Aeges.Storage`.
- [x] Define `ITaskRepository`.
- [x] Define `IIterationRepository`.
- [x] Define `IArtifactRepository`.
- [x] Define `IApprovalRepository`.
- [x] Define `IProjectRepository`.
- [x] Define `IMachineRepository`.
- [x] Define `ILockRepository`.
- [x] Define `IRunnerExecutionRepository`.
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
- [x] Test runner execution persistence behavior.

## Application Orchestration

- [x] Add project registration use case.
- [x] Add machine registration and heartbeat use cases.
- [x] Add task creation use case.
- [x] Add task query use cases.
- [x] Add bounded task iteration creation use case.
- [x] Add task status transition use cases.
- [x] Add task cancellation use case.
- [x] Add approval request and resolution use cases.
- [x] Add artifact registration use cases.
- [x] Add governance checks before runner dispatch.
- [x] Add durable runner execution tracking use case.
- [x] Add deterministic runner request creation use case.
- [x] Add structured result models for expected runtime failures.

## Runner Contracts

- [x] Add mock runner for tests.
- [x] Add Codex runner project contracts without launching Codex yet.
- [x] Add Codex CLI runner process shell.
- [x] Add cancellation and timeout contract coverage.

## Git Foundation

- [x] Add repository detection contract.
- [x] Add worktree creation contract.
- [x] Add git status and diff contracts.
- [x] Add base commit recording model.
- [x] Add path safety checks for worktree paths.
- [x] Test worktree path generation and validation.
- [x] Add process-backed Git runtime for repository detection, status, diff,
      base commit capture, and worktree creation.

## Runtime And CLI

- [x] Package `Aeges.Cli` as a .NET global/local tool.
- [x] Add runtime directory model for `~/.aeges`.
- [x] Add config loading model.
- [x] Add `aeges db status`.
- [x] Add `aeges db migrate`.
- [x] Add `aeges task create`.
- [x] Add `aeges task status`.
- [x] Add `aeges task cancel`.
- [x] Add `aeges agent run` host shell.
- [x] Make local agent claim one queued task assigned to its machine.
- [x] Make local agent write and register prompt artifacts for claimed
      iterations.
- [x] Add opt-in local agent Git worktree creation for claimed iterations.
- [x] Add opt-in mock runner execution from the local agent.
- [x] Add opt-in Codex runner resolution gated by worktree creation.
- [x] Add `aeges agent start/status/stop` process wrapper for background queue
      processing.
- [x] Keep CLI usable without Telegram or a control plane.

## Telegram Transport

- [x] Add button-first Telegram interaction handler.
- [x] Add stable callback payloads for menu, projects, machines, queued tasks,
      and task details.
- [x] Add Telegram application facade boundary so transport code does not own
      workflow logic.
- [x] Add authorization check for configured Telegram chat IDs.
- [x] Add tests for button navigation, authorization, and task-detail callbacks.
- [x] Add live Telegram Bot API gateway around the button interaction handler.
- [x] Add testable Telegram long-polling service.
- [x] Add CLI host command for running Telegram long polling.
- [x] Add approval-resolution buttons after approval workflow is exposed through
      the facade.
- [x] Add task cancellation buttons from Telegram task details.
- [x] Add button-driven Telegram task creation flow.
- [x] Add count badges to Telegram menu buttons.
- [x] Add Telegram task status buckets and watched-task status notifications.
- [x] Add project-scoped Telegram task browsing from the Projects page.
- [x] Add Telegram reviewing-task continuation with durable review feedback.
- [x] Start the local agent automatically after Telegram queues or continues a
      task.
- [x] Add current-process Telegram token prompt for `aeges telegram run`.
- [x] Add `aeges telegram setup` wizard for config, token source, and chat IDs.
- [x] Add `aeges telegram check` for bot identity preflight.
- [x] Remove environment-variable-name prompt from Telegram setup wizard.
- [x] Edit Telegram callback messages in place to reduce menu spam.
- [x] Add `aeges telegram start/status/stop` process wrapper with pid metadata.
- [x] Add `aeges agent restart` and `aeges telegram restart` convenience commands.

## Governance

- [x] Add governance policy model.
- [x] Add approval-required decisions for dependency changes.
- [x] Add approval-required decisions for migrations.
- [x] Add approval-required decisions for destructive git operations.
- [x] Add allowed and denied path policy checks.
- [x] Add max iteration and timeout policy checks.
- [x] Add lock conflict detection.

## Operator CLI

- [x] Add `aeges task continue` parity for Telegram task continuation.
- [ ] Add `aeges task review` to show runner response, iterations, and
      artifacts without Telegram.
- [ ] Add `aeges task artifacts` to list artifact metadata and local paths.

## Documentation

- [x] Add first-user README with local CLI, agent, runner, and Telegram setup.
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
