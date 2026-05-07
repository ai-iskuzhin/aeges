# AGENTS.md

# Aeges

**Reliable Governed Coding Runtime**

Aeges is a governed execution runtime for AI-assisted software development.

Aeges is not an autonomous coding agent. It is the runtime layer above coding agents.

Aeges owns:

- orchestration
- governance
- durable state
- task lifecycle
- approvals
- artifacts
- reliability
- remote execution

LLMs and coding agents are replaceable workers.

```text
LLMs are workers.
Aeges owns execution.
```

---

# Repository Structure

Use this repository layout:

```text
/
├── src/
│   ├── Aeges.Core/
│   ├── Aeges.Application/
│   ├── Aeges.Agent/
│   ├── Aeges.Cli/
│   ├── Aeges.Telegram/
│   ├── Aeges.Runners/
│   ├── Aeges.Runners.Codex/
│   ├── Aeges.Storage/
│   ├── Aeges.Storage.Sqlite/
│   ├── Aeges.Git/
│   └── Aeges.ControlPlane/
│
├── tests/
│   ├── Aeges.Core.Tests/
│   ├── Aeges.Application.Tests/
│   ├── Aeges.Agent.Tests/
│   ├── Aeges.Runners.Tests/
│   ├── Aeges.Storage.Tests/
│   ├── Aeges.Storage.Sqlite.Tests/
│   ├── Aeges.Git.Tests/
│   └── Aeges.IntegrationTests/
│
├── docs/
│   ├── architecture.md
│   ├── roadmap.md
│   ├── task-model.md
│   ├── sqlite.md
│   ├── runtime-layout.md
│   ├── runners.md
│   ├── governance.md
│   └── security.md
│
├── samples/
│   ├── config/
│   ├── prompts/
│   ├── tasks/
│   └── sqlite/
│
├── scripts/
│   ├── install-windows.ps1
│   ├── install-linux.sh
│   └── install-macos.sh
│
├── .github/
│   └── workflows/
│       └── ci.yml
│
├── AGENTS.md
├── README.md
├── ROADMAP.md
├── LICENSE
├── Directory.Build.props
├── Directory.Packages.props
├── global.json
└── Aeges.sln
```

---

# Project Responsibilities

## `Aeges.Core`

Core domain model and contracts.

Contains:

- task model
- task lifecycle
- iteration model
- artifact model
- approval model
- lock model
- machine model
- project model
- runner abstractions
- governance policies
- domain events
- domain errors

Must not depend on:

- Telegram
- Codex
- Claude
- SQLite
- ASP.NET
- OS-specific services

---

## `Aeges.Application`

Application orchestration layer.

Responsible for:

- task creation
- task scheduling
- state transitions
- workflow coordination
- approval handling
- runner dispatch
- artifact coordination
- cancellation
- retries
- governance enforcement

This layer coordinates domain logic and infrastructure through interfaces.

---

## `Aeges.Agent`

Local background runtime.

Responsible for:

- running as a daemon/service
- polling for local tasks
- executing assigned tasks
- managing runtime directories
- coordinating runners
- writing artifacts
- enforcing local governance
- reporting machine status

Target hosts:

- Windows Service
- systemd
- launchd

---

## `Aeges.Cli`

Developer/operator CLI.

Example commands:

```text
aeges init
aeges agent run
aeges agent install
aeges agent status
aeges project add
aeges project list
aeges task create
aeges task status
aeges task cancel
aeges task artifacts
aeges db migrate
aeges db status
```

CLI must work without Telegram or a control plane.

---

## `Aeges.Telegram`

Telegram transport and user interface.

Responsible for:

- receiving user commands
- listing projects
- creating tasks
- showing task status
- requesting approvals
- sending artifacts
- routing user replies

Telegram is a transport layer.

Telegram must not own workflow logic.

---

## `Aeges.Runners`

Runner abstractions and shared runner utilities.

Potential runners:

- Codex CLI
- Claude Code
- Aider
- OpenHands
- mock runner for tests

---

## `Aeges.Runners.Codex`

Codex CLI runner implementation.

Responsible for:

- building Codex execution requests
- launching Codex CLI
- capturing stdout/stderr
- enforcing timeout
- supporting cancellation
- writing runner artifacts
- reporting exit status

---

## `Aeges.Storage`

Storage abstractions.

Contains interfaces such as:

- `ITaskRepository`
- `IIterationRepository`
- `IArtifactRepository`
- `IApprovalRepository`
- `IProjectRepository`
- `IMachineRepository`
- `ILockRepository`
- `IUnitOfWork`

No direct SQLite dependency should be placed here.

---

## `Aeges.Storage.Sqlite`

SQLite persistence implementation.

Responsible for:

- SQLite schema
- migrations
- repository implementations
- transactional updates
- local durable task state
- runtime event storage
- lock persistence
- artifact metadata persistence

SQLite is the default local storage engine for MVP.

---

## `Aeges.Git`

Git integration.

Responsible for:

- repository detection
- worktree creation
- status checks
- diff generation
- patch generation
- base commit recording
- rollback helpers
- reconciliation helpers

---

## `Aeges.ControlPlane`

Future centralized orchestration service.

Responsible for:

- multi-machine coordination
- web API
- dashboard
- agent registration
- heartbeats
- remote task routing
- centralized governance
- artifact browsing

This project may stay experimental during MVP.

---

# Dependency Direction

Allowed dependency direction:

```text
Aeges.Core
  ↑
Aeges.Application
  ↑
Aeges.Storage
  ↑
Aeges.Storage.Sqlite

Aeges.Core
  ↑
Aeges.Runners
  ↑
Aeges.Runners.Codex

Aeges.Core
  ↑
Aeges.Git

Aeges.Application
  ↑
Aeges.Agent
Aeges.Cli
Aeges.Telegram
Aeges.ControlPlane
```

Rules:

- `Aeges.Core` must remain infrastructure-free.
- `Aeges.Application` coordinates use cases through interfaces.
- Infrastructure projects implement interfaces.
- UI/transport projects must not contain domain rules.
- Storage implementations must not leak SQLite types into core/application APIs.

---

# SQLite Guidelines

SQLite is the default local persistence layer for MVP.

Use SQLite for:

- local task queue
- task lifecycle state
- iterations
- artifact metadata
- approval requests
- project registry
- machine metadata
- locks
- runtime events
- runner execution records

Do not store large artifact contents directly in SQLite by default.

Store large content as files and keep metadata in SQLite.

Examples of file-based artifacts:

- stdout logs
- stderr logs
- diffs
- patches
- prompts
- AI results
- test outputs

SQLite stores:

- artifact ID
- task ID
- iteration ID
- artifact type
- relative path
- size
- checksum
- created timestamp

---

## SQLite Runtime Path

Default local runtime directory:

```text
~/.aeges/
```

Default database path:

```text
~/.aeges/aeges.db
```

Runtime layout:

```text
~/.aeges/
├── aeges.db
├── logs/
├── runs/
├── worktrees/
├── artifacts/
└── config.json
```

---

## SQLite Schema Ownership

SQLite schema belongs to `Aeges.Storage.Sqlite`.

Schema definitions should be represented by EF Core entity configuration and
versioned through EF Core migrations.

Suggested location:

```text
src/Aeges.Storage.Sqlite/
├── Migrations/
├── AegesDbContext.cs
├── AegesDbContextFactory.cs
├── EntityConfigurations/
└── Repositories/
```

---

## SQLite Migration Rules

Use EF Core migrations rather than a custom SQL migration runner.

Migrations should be:

- ordered
- transactional
- recorded through EF Core migration history
- safe to run at agent startup

Migration commands:

```text
dotnet ef migrations add <Name> --project src/Aeges.Storage.Sqlite --startup-project src/Aeges.Cli
dotnet ef database update --project src/Aeges.Storage.Sqlite --startup-project src/Aeges.Cli
```

CLI commands such as `aeges db status` and `aeges db migrate` may wrap EF Core
migration APIs. Agent startup may run migrations automatically for local SQLite
when configured to do so.

---

## Suggested Initial SQLite Tables

Minimum MVP tables:

```text
projects
tasks
task_iterations
artifacts
approvals
locks
runtime_events
runner_executions
machines
```

---

## Suggested Initial Schema

```sql
CREATE TABLE projects (
    id TEXT PRIMARY KEY,
    name TEXT NOT NULL,
    path TEXT NOT NULL,
    created_at TEXT NOT NULL,
    updated_at TEXT NOT NULL
);

CREATE TABLE tasks (
    id TEXT PRIMARY KEY,
    project_id TEXT NOT NULL,
    machine_id TEXT NOT NULL,
    title TEXT NOT NULL,
    goal TEXT NOT NULL,
    status TEXT NOT NULL,
    priority INTEGER NOT NULL DEFAULT 0,
    max_iterations INTEGER NOT NULL DEFAULT 3,
    current_iteration INTEGER NOT NULL DEFAULT 0,
    created_at TEXT NOT NULL,
    updated_at TEXT NOT NULL,
    started_at TEXT NULL,
    completed_at TEXT NULL,
    cancelled_at TEXT NULL,
    failure_reason TEXT NULL,
    FOREIGN KEY (project_id) REFERENCES projects(id)
);

CREATE TABLE task_iterations (
    id TEXT PRIMARY KEY,
    task_id TEXT NOT NULL,
    iteration_number INTEGER NOT NULL,
    status TEXT NOT NULL,
    runner_id TEXT NOT NULL,
    worktree_path TEXT NULL,
    prompt_artifact_id TEXT NULL,
    result_artifact_id TEXT NULL,
    diff_artifact_id TEXT NULL,
    started_at TEXT NULL,
    completed_at TEXT NULL,
    failure_reason TEXT NULL,
    FOREIGN KEY (task_id) REFERENCES tasks(id)
);

CREATE TABLE artifacts (
    id TEXT PRIMARY KEY,
    task_id TEXT NOT NULL,
    iteration_id TEXT NULL,
    type TEXT NOT NULL,
    relative_path TEXT NOT NULL,
    size_bytes INTEGER NULL,
    sha256 TEXT NULL,
    created_at TEXT NOT NULL,
    FOREIGN KEY (task_id) REFERENCES tasks(id),
    FOREIGN KEY (iteration_id) REFERENCES task_iterations(id)
);

CREATE TABLE approvals (
    id TEXT PRIMARY KEY,
    task_id TEXT NOT NULL,
    iteration_id TEXT NULL,
    status TEXT NOT NULL,
    reason TEXT NOT NULL,
    requested_action TEXT NOT NULL,
    created_at TEXT NOT NULL,
    resolved_at TEXT NULL,
    resolved_by TEXT NULL,
    FOREIGN KEY (task_id) REFERENCES tasks(id),
    FOREIGN KEY (iteration_id) REFERENCES task_iterations(id)
);

CREATE TABLE locks (
    id TEXT PRIMARY KEY,
    task_id TEXT NOT NULL,
    project_id TEXT NOT NULL,
    path_pattern TEXT NOT NULL,
    created_at TEXT NOT NULL,
    released_at TEXT NULL,
    FOREIGN KEY (task_id) REFERENCES tasks(id),
    FOREIGN KEY (project_id) REFERENCES projects(id)
);

CREATE TABLE runtime_events (
    id TEXT PRIMARY KEY,
    task_id TEXT NULL,
    iteration_id TEXT NULL,
    machine_id TEXT NULL,
    event_type TEXT NOT NULL,
    message TEXT NOT NULL,
    payload_json TEXT NULL,
    created_at TEXT NOT NULL
);

CREATE TABLE runner_executions (
    id TEXT PRIMARY KEY,
    task_id TEXT NOT NULL,
    iteration_id TEXT NOT NULL,
    runner_id TEXT NOT NULL,
    command TEXT NOT NULL,
    working_directory TEXT NOT NULL,
    exit_code INTEGER NULL,
    started_at TEXT NOT NULL,
    completed_at TEXT NULL,
    timed_out INTEGER NOT NULL DEFAULT 0,
    cancelled INTEGER NOT NULL DEFAULT 0,
    FOREIGN KEY (task_id) REFERENCES tasks(id),
    FOREIGN KEY (iteration_id) REFERENCES task_iterations(id)
);

CREATE TABLE machines (
    id TEXT PRIMARY KEY,
    name TEXT NOT NULL,
    platform TEXT NOT NULL,
    status TEXT NOT NULL,
    last_seen_at TEXT NULL,
    created_at TEXT NOT NULL,
    updated_at TEXT NOT NULL
);
```

---

## SQLite Access

Preferred for MVP:

```text
EF Core SQLite
```

Reason:

- first-class migrations through `dotnet ef`
- explicit model configuration
- transactional unit-of-work support
- less custom migration infrastructure
- predictable local runtime behavior

---

## SQLite Concurrency

SQLite is local-first storage.

Rules:

- use one local agent process per database
- enable WAL mode
- use transactions for state changes
- avoid long-running write transactions
- keep artifacts in filesystem
- store only metadata in SQLite

Recommended pragmas:

```sql
PRAGMA journal_mode = WAL;
PRAGMA foreign_keys = ON;
PRAGMA busy_timeout = 5000;
```

---

## SQLite Repository Rules

Repository methods must:

- accept `CancellationToken`
- use transactions for multi-step updates
- never return provider-specific SQLite types
- map persistence rows into domain/application models
- keep SQL close to repository implementation
- avoid hidden global connections

---

# Dependency Injection

Use Microsoft.Extensions.DependencyInjection.

Prefer explicit registration methods:

```csharp
services.AddAegesCore();
services.AddAegesApplication();
services.AddAegesSqliteStorage();
services.AddAegesRunners();
services.AddAegesCodexRunner();
services.AddAegesTelegram();
```

Avoid static service locators.

---

# Configuration

Use Microsoft.Extensions.Configuration.

Supported config sources:

- `appsettings.json`
- environment variables
- command-line args
- user-level config file

Recommended local config path:

```text
~/.aeges/config.json
```

Example config:

```json
{
  "machineId": "home-laptop",
  "storage": {
    "provider": "sqlite",
    "connectionString": "Data Source=/home/user/.aeges/aeges.db"
  },
  "telegram": {
    "botTokenEnvironmentVariable": "AEGES_TELEGRAM_BOT_TOKEN",
    "allowedChatIds": []
  },
  "runners": {
    "default": "codex",
    "codex": {
      "executable": "codex",
      "timeoutSeconds": 1800
    }
  },
  "projects": [
    {
      "id": "aeges",
      "name": "Aeges",
      "path": "/home/user/dev/aeges"
    }
  ]
}
```

Never commit real local config, tokens, or machine-specific secrets.

---

# Coding Style

Prefer:

- small classes
- explicit names
- immutable value objects where practical
- cancellation-aware async APIs
- structured logging
- deterministic behavior
- clear domain boundaries
- XML documentation comments on public types, public members, and interfaces that form domain, application, storage, runner, or transport contracts

Avoid:

- global mutable state
- hidden background work
- fire-and-forget tasks
- broad exception swallowing
- model-specific assumptions
- transport-specific domain logic
- noisy XML comments on private implementation details that are already self-explanatory

---

# Async Guidelines

All long-running operations must accept `CancellationToken`.

Examples:

```csharp
Task<TaskRunResult> RunAsync(TaskRunRequest request, CancellationToken cancellationToken);
Task SaveAsync(RuntimeTask task, CancellationToken cancellationToken);
```

Do not block async code with:

```csharp
.Result
.Wait()
GetAwaiter().GetResult()
```

---

# Logging Guidelines

Use `ILogger<T>`.

Logs should include:

- task ID
- machine ID
- project ID
- runner ID
- iteration number
- correlation ID when available

Avoid logging:

- secrets
- tokens
- full private prompts unless explicitly configured
- environment variables
- credentials

---

# Error Handling

Prefer explicit result types for expected runtime failures.

Examples:

- task cancelled
- approval required
- timeout
- runner unavailable
- project not found
- lock conflict
- worktree creation failed
- SQLite migration failed
- SQLite lock timeout

Unexpected failures may throw exceptions, but must be logged and reflected in task state.

---

# Domain Model Guidelines

## IDs

Use strongly typed IDs where practical.

Examples:

```csharp
TaskId
ProjectId
MachineId
RunId
IterationId
ArtifactId
ApprovalId
RunnerId
LockId
```

Avoid passing raw strings everywhere.

---

## Task Lifecycle

Supported lifecycle states:

```text
queued
planning
running
reviewing
waiting_approval
completed
failed
cancelled
```

State transitions must be explicit.

Invalid transitions must be rejected.

---

## Iterations

A task may have multiple iterations.

Each iteration should store:

- prompt
- runner
- start time
- end time
- status
- stdout log
- stderr log
- result
- diff
- review
- approval request if any

---

## Artifacts

Artifacts are first-class entities.

Artifact types:

```text
prompt
plan
stdout_log
stderr_log
result
diff
review
approval_request
test_output
metadata
```

Artifacts should be addressable by ID and path.

---

# Runner Guidelines

## Runner Interface

All AI execution backends should implement an interface similar to:

```csharp
public interface IAegesRunner
{
    string Id { get; }

    Task<RunnerResult> RunAsync(
        RunnerRequest request,
        CancellationToken cancellationToken);
}
```

Runner requests should include:

- task ID
- project path
- worktree path
- prompt path
- artifact output directory
- timeout
- environment variables
- allowed tools or policy hints

Runner results should include:

- exit code
- status
- stdout path
- stderr path
- result artifact path
- produced artifact paths
- error summary

---

## Codex CLI Runner

Codex runner should:

- execute inside task worktree
- capture stdout and stderr
- enforce timeout
- write result artifacts
- never assume interactive input is available
- support cancellation
- report non-zero exit codes clearly

---

## Mock Runner

A mock runner must exist for tests.

It should support:

- success
- failure
- timeout simulation
- approval request simulation
- artifact generation

---

# Git Guidelines

## Worktrees

Prefer one worktree per task or iteration.

Worktree directory example:

```text
~/.aeges/worktrees/
  <project-id>/
    <task-id>/
```

Before execution:

- verify repository state
- create worktree
- record base commit
- record branch name

After execution:

- capture `git status`
- capture `git diff`
- store patch artifact
- record changed files

---

## Git Safety

Never run destructive git commands unless explicitly approved.

Commands requiring approval:

- `git reset --hard`
- `git clean`
- `git push`
- branch deletion
- force push
- destructive merge/rebase operations

---

# Governance Guidelines

## Runtime Limits

Every task should support:

- maximum runtime
- maximum iterations
- maximum changed files
- maximum output size
- allowed paths
- denied paths
- approval policy

---

## Approval Gates

Require approval for:

- dependency changes
- package manager lockfile changes
- database migrations
- deleting files
- modifying CI/CD
- deployment scripts
- secrets/config files
- git push
- destructive shell commands

---

## Path Safety

Tasks must not modify files outside approved project roots.

Paths must be normalized and validated.

Symlink traversal must be considered.

---

# Locking Guidelines

Start with lightweight path-based locks.

Example:

```json
{
  "taskId": "task-001",
  "locks": [
    "src/Auth/*",
    "package.json"
  ]
}
```

Locks should prevent conflicting concurrent task execution.

MVP does not require semantic code-region locks.

---

# Testing Guidelines

## Test Framework

Use xUnit unless there is a strong reason to choose otherwise.

Recommended:

- xUnit
- FluentAssertions
- NSubstitute
- Microsoft.NET.Test.Sdk

---

## Test Types

Use:

```text
tests/Aeges.Core.Tests
tests/Aeges.Application.Tests
tests/Aeges.Agent.Tests
tests/Aeges.Runners.Tests
tests/Aeges.Storage.Tests
tests/Aeges.Storage.Sqlite.Tests
tests/Aeges.Git.Tests
tests/Aeges.IntegrationTests
```

---

## Unit Tests

Unit tests should cover:

- task lifecycle transitions
- approval policy decisions
- lock conflict detection
- artifact path generation
- runner request construction
- governance limits
- ID/value object behavior

---

## SQLite Tests

SQLite tests should cover:

- EF Core schema creation against a temporary database
- task CRUD
- task state transitions
- iteration persistence
- artifact metadata persistence
- approval persistence
- lock acquisition and release
- runtime event append/read
- transaction rollback on failure
- WAL/foreign key pragma initialization

SQLite tests should use temporary database files.

Avoid relying only on in-memory SQLite because file locking and WAL behavior differ.

---

## Integration Tests

Integration tests may cover:

- SQLite persistence
- worktree creation
- runner execution with mock runner
- artifact storage
- CLI command behavior

Integration tests must not require real Codex/Claude credentials.

---

## External Runner Tests

Tests involving real AI runners must be opt-in.

They should be skipped by default unless explicit environment variables are set.

Example:

```text
AEGES_RUN_CODEX_TESTS=true
```

---

# Security Guidelines

Never commit:

- Telegram bot tokens
- Codex credentials
- Claude credentials
- API keys
- SSH keys
- private project paths
- machine-specific config
- SQLite databases containing real task history

Secrets should come from:

- environment variables
- local untracked config
- OS secret store in the future

---

# Cross-Platform Guidelines

Aeges must support:

- Windows
- macOS
- Linux

Avoid:

- hardcoded path separators
- Windows-only shell assumptions
- PowerShell-only runtime logic
- Bash-only runtime logic
- registry-dependent core behavior

Use:

- `Path.Combine`
- `Environment.SpecialFolder`
- `System.Diagnostics.Process`
- OS abstractions where needed

---

# CLI Guidelines

CLI output should be:

- human-readable by default
- optionally machine-readable with `--json`
- deterministic where possible

Commands should return meaningful exit codes.

---

# Documentation Requirements

When adding major features, update relevant docs:

```text
README.md
ROADMAP.md
docs/architecture.md
docs/task-model.md
docs/sqlite.md
docs/runtime-layout.md
docs/governance.md
docs/security.md
```

Public docs should emphasize:

```text
Aeges is not an autonomous coding agent.
Aeges is a governed runtime for coding agents.
```

---

# Pull Request Guidelines

Every meaningful change should include:

- clear description
- tests when applicable
- documentation updates when behavior changes
- no unrelated formatting churn
- no secrets or machine-specific config
- no committed local SQLite runtime databases

---

# MVP Priorities

Focus on:

1. durable SQLite task model
2. local agent runtime
3. Codex CLI runner
4. artifact storage
5. Telegram interface
6. git worktree isolation
7. basic approvals
8. cancellation and timeouts
9. structured logs
10. tests for core domain logic
11. SQLite migrations and repository tests

Do not overbuild early:

- full control plane
- semantic knowledge graph
- complex scheduler
- multi-agent swarm
- web dashboard
- distributed artifact storage

---

# Non-Goals

Aeges is not:

- a chat wrapper
- an autonomous AI swarm
- a model-specific agent
- a prompt-only framework
- a replacement for Git
- a replacement for CI
- a deployment automation system

---

# Core Philosophy

```text
LLMs are workers.
Aeges owns execution.
```

```text
Trust the runtime, not the model.
```

```text
Deterministic orchestration over autonomous improvisation.
```
