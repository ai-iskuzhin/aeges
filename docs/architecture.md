# Aeges Architecture

Aeges is a reliable governed coding runtime. It owns orchestration, governance,
durable state, approvals, artifacts, retries, rollback, iteration lifecycle, and
remote execution coordination.

LLMs and coding agents are replaceable workers. The runtime remains
authoritative for task lifecycle, scheduling, approvals, cancellation, artifact
persistence, and bounded execution.

## Project Boundaries

- `Aeges.Core` contains infrastructure-free domain models and contracts.
- `Aeges.Application` coordinates workflows through abstractions.
- `Aeges.Storage` defines storage abstractions.
- `Aeges.Storage.Sqlite` implements local SQLite persistence.
- `Aeges.Runners` defines runner abstractions and shared utilities.
- `Aeges.Runners.Codex` implements the Codex CLI runner.
- `Aeges.Git` owns repository and worktree integration contracts.
- `Aeges.Agent`, `Aeges.Cli`, and `Aeges.Telegram` are hosts or transports.

## Core Runtime Concepts

The initial core model includes strongly typed identifiers plus durable models
for tasks, iterations, artifacts, approvals, projects, machines, and
path-based locks. These types describe runtime state and validation rules only;
they do not depend on persistence, transports, runners, or operating-system
services.

## Runner Boundary

`Aeges.Runners` defines replaceable worker contracts in terms of core runtime
identifiers. Runner implementations report execution results and artifacts; they
do not own task lifecycle transitions or orchestration decisions.

## Git Boundary

`Aeges.Git` defines provider-neutral contracts and models for repository
detection, worktree creation, status capture, diff capture, and base commit
recording. It also builds deterministic worktree paths under the configured
runtime worktree root. Actual Git command execution is left to later
infrastructure slices.

## Storage Boundary

`Aeges.Storage` defines provider-neutral repository and unit-of-work contracts
over core domain models. EF Core and SQLite implementation details belong only
to `Aeges.Storage.Sqlite`.

## Application Boundary

`Aeges.Application` exposes use-case services for project registration, machine
registration and heartbeat reporting, durable task creation and lookup, and
explicit task lifecycle transitions. It also coordinates approval gates:
requesting approval pauses the task, approval resumes it, rejection fails it,
and cancellation cancels it. Artifact registration records durable metadata and
links prompt, result, and diff artifacts back to their owning iterations.
Governance preflight evaluates runner dispatch against runtime-owned policy
before execution is allowed to proceed. Path-based lock acquisition detects
active conflicts before persisting new locks.
Expected failures, such as missing projects, machines, tasks, iterations,
approvals, invalid transitions, unsafe artifact paths, or iteration limit
violations, are returned as structured application results instead of being
hidden in transport-specific responses. The application layer coordinates
repositories through `IUnitOfWork`; it does not depend on SQLite or any runner
implementation.

## Dependency Direction

Dependencies point inward toward stable abstractions. Infrastructure implements
interfaces and must not leak provider-specific details into core or application
APIs.

Core must remain free of SQLite, Telegram, Codex, ASP.NET, and OS-specific
services.
