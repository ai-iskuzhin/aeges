# ROADMAP.md

# Aeges Roadmap

## Vision

Aeges aims to become a reliable governed runtime for AI-assisted software development.

The project focuses on:

- deterministic orchestration
- governed execution
- durable workflows
- remote runtime management
- repository-aware execution
- human-supervised AI coding systems

Aeges is not an autonomous coding agent.

Aeges is an execution runtime above coding agents.

---

# Core Long-Term Goals

## Reliability

Execution must be:

- durable
- observable
- replayable
- auditable
- interruptible
- recoverable

---

## Governance

The runtime must own:

- approvals
- limits
- retries
- rollback
- orchestration
- execution permissions
- iteration control

---

## AI Replaceability

The system must support interchangeable runners.

Potential runners:

- Codex CLI
- Claude Code
- Aider
- OpenHands
- local LLM runtimes
- future coding systems

---

## Remote Runtime Infrastructure

Aeges should support:

- distributed agents
- remote execution
- multi-machine orchestration
- durable synchronization
- offline recovery
- governed control planes

---

# Phase 1 — MVP Runtime

Status: Planned

Goal:
Build a reliable single-machine governed coding runtime.

---

## Objectives

- local runtime execution
- Telegram-based control interface
- durable task lifecycle
- Codex CLI integration
- isolated execution
- structured artifacts

---

## Features

### Agent Runtime

- background runtime service
- cross-platform .NET worker
- local task execution
- configurable projects
- structured runtime directories

---

### Telegram Integration

- Telegram bot interface
- project selection
- task submission
- status tracking
- artifact delivery
- approval prompts

---

### Telegram Threaded Work

Status: In progress

Goal:
Let Telegram act as a natural multi-surface operator UI without making Telegram
the source of truth.

Principles:

- BotFather and Telegram permissions are an outer gate only.
- Aeges user, project, group, task, and approval permissions remain the runtime
  gate.
- `chat_id + message_thread_id` is transport routing metadata, not task state.
- Aeges persists all task and talk bindings in SQLite.
- Task execution remains owned by the local agent and bounded by project
  parallelism rules.

Stages:

1. Private thread routing for direct messages.
   - Opt in with `telegram.enablePrivateChatThreads`.
   - Scope task drafts, task bindings, watched task details, and talk sessions
     by private `message_thread_id`.
   - Start task creation from private threads with `@bot new task ...`.

2. Forum-topic task collaboration.
   - Keep supergroup topic support for team tasks.
   - Update topic titles with task status.
   - Require replies to bot prompts for group task continuation.

3. Inline mode.
   - Add read-only inline search for projects and tasks.
   - Return guarded action cards instead of performing lifecycle changes from
     inline query text alone.
   - Reuse existing callback-token storage for inline actions.

4. Live progress.
   - Use persisted runtime events as the source of operator-visible progress.
   - Edit watched task-detail messages when progress changes.
   - Consider Telegram draft updates only as temporary UI, never as durable
     task output.

---

### Task System

- durable SQLite-backed tasks
- explicit task lifecycle
- iteration tracking
- retries
- cancellation
- structured metadata

---

### Artifact System

Store:

- prompts
- logs
- patches
- reviews
- approvals
- runtime metadata
- execution outputs

Example:

```text
runs/
task-001/
task.json
prompt.md
stdout.log
stderr.log
result.md
diff.patch
```

---

### Git Worktree Isolation

- one worktree per task
- isolated execution
- rollback support
- preserved diffs

---

### Basic Governance

- iteration limits
- execution timeout
- allowed project paths
- approval checkpoints
- command restrictions

---

## Deliverables

- working runtime daemon
- Telegram-controlled execution
- Codex runner
- durable task storage
- worktree isolation
- structured artifact persistence

---

## Distribution Roadmap

Status: In progress

Goal:
Make `aeges` easy to install while keeping runtime setup explicit and governed.

### Stage 1: .NET Tool

- package `Aeges.Cli` as a .NET tool
- expose the command name `aeges`
- support local checkout installation through `dotnet pack` and `dotnet tool install`
- later publish the tool package to NuGet

### Stage 2: Release Artifacts And Shell Installer

- publish versioned GitHub release artifacts
- publish checksums for release artifacts
- add a macOS/Linux shell installer
- avoid `sudo` by default
- install into `~/.aeges/bin` or `~/.local/bin`

### Stage 3: Homebrew

- create a Homebrew tap
- install from checksummed release archives
- support macOS first and Linuxbrew where practical

### Stage 4: Native Package Managers

- Windows: winget, Scoop, and possibly MSI
- Linux: deb/rpm packages after daemon/service behavior stabilizes
- macOS: Homebrew plus launchd service helper

The .NET tool remains the developer-friendly and cross-platform baseline even
after native package managers are added.

---

# Phase 2 — Multi-Machine Runtime

Status: Planned

Goal:
Support distributed governed execution across multiple machines.

---

## Objectives

- multiple remote agents
- machine-aware orchestration
- distributed task routing
- runtime synchronization

---

## Features

### Machine Registry

Track:

- machine ID
- platform
- capabilities
- status
- heartbeat
- runner availability

---

### Distributed Routing

- route tasks to machines
- machine-aware scheduling
- task ownership
- remote execution coordination

---

### Runtime Synchronization

- task sync
- artifact sync
- recovery after reconnect
- durable execution continuity

---

### Cross-Platform Runtime

Support:

- Windows
- macOS
- Linux

Execution environments:

- Windows Services
- launchd
- systemd

---

## Deliverables

- multi-machine coordination
- remote task execution
- runtime synchronization
- distributed orchestration primitives

---

# Phase 3 — Governed Workflow Engine

Status: Planned

Goal:
Introduce deterministic multi-step workflow orchestration.

---

## Objectives

- structured workflow execution
- role-based orchestration
- bounded iterative loops
- approval-aware workflows

---

## Features

### Workflow Graphs

Support workflow stages:

- planning
- execution
- review
- testing
- approval
- reconciliation

---

### Role-Based Agents

Potential roles:

- Architect
- Executor
- Reviewer
- Tester

---

### Iteration Control

- bounded loops
- retry policies
- rollback policies
- failure recovery
- deterministic stopping conditions

---

### Human-in-the-Loop

Support:

- explicit approvals
- interactive reviews
- pause/resume
- runtime intervention

---

## Deliverables

- workflow engine
- role orchestration
- governed iteration loops
- human-supervised execution

---

# Phase 4 — Repository-Aware Runtime

Status: Planned

Goal:
Introduce lightweight repository intelligence.

---

## Objectives

- safer execution
- better scheduling
- conflict prediction
- repository-aware orchestration

---

## Features

### Lightweight Repository Intelligence

- imports
- dependency mapping
- git history analysis
- affected-file prediction
- changed-together analysis

---

### Tree-Sitter Integration

Potential capabilities:

- lightweight AST parsing
- symbol extraction
- file relationships
- incremental indexing

---

### Scheduling Improvements

- smarter task routing
- conflict reduction
- repository-aware locks
- execution planning

---

## Non-Goals

Avoid early complexity:

- full semantic knowledge graph
- heavy indexing infrastructure
- Sourcegraph-scale architecture
- complex code-region locking

---

## Deliverables

- repository-aware execution
- lightweight indexing
- smarter scheduling
- improved parallel safety

---

# Phase 5 — Control Plane

Status: Future

Goal:
Build centralized governed runtime orchestration.

---

## Objectives

- centralized orchestration
- runtime observability
- distributed governance
- operational tooling

---

## Features

### Control Plane

- web UI
- runtime dashboard
- distributed orchestration
- task monitoring
- artifact browsing

---

### Observability

Track:

- runtime metrics
- task history
- execution logs
- agent health
- workflow timelines

---

### Governance Policies

Potential policies:

- execution permissions
- path restrictions
- approval requirements
- runtime quotas
- scheduling limits

---

### Distributed Artifact Storage

Potential support:

- shared runtime storage
- task archival
- replay support
- audit retention

---

## Deliverables

- centralized control plane
- runtime observability
- governance tooling
- operational runtime management

---

# Technical Direction

## Language

Primary stack:

- .NET
- C#
- Generic Host
- ASP.NET Core
- SQLite/Postgres

---

## Runtime Philosophy

The runtime owns:

- state
- orchestration
- governance
- approvals
- retries
- lifecycle
- observability

AI systems execute tasks within governed constraints.

---

# Strategic Direction

Aeges should evolve into:

```text
Reliable Governed Coding Runtime
```

Not:

```text
autonomous AI coding swarm
```

---

# Core Principle

```text
LLMs are workers.
Aeges owns execution.
```
