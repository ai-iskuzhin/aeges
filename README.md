# AGENTS.md

## Aeges

**Reliable Governed Coding Runtime**

Aeges is not an autonomous coding agent.

Aeges is a governed execution runtime for coding agents such as Codex CLI, Claude Code, Aider, OpenHands, and future AI-assisted development systems.

The system owns:

- execution
- governance
- orchestration
- state
- approvals
- artifacts
- reliability
- iteration lifecycle

LLMs are replaceable workers.

---

# Core Principles

## Governance First

Aeges does not blindly trust AI agents.

All execution must be:

- observable
- reproducible
- auditable
- interruptible
- reviewable

The runtime owns control.

---

## Deterministic Orchestration

Execution should be deterministic whenever possible.

Aeges favors:

- explicit task lifecycle
- structured artifacts
- durable state
- replayable execution
- isolated workspaces
- explicit approvals
- constrained iteration loops

Over:

- autonomous long-running conversations
- hidden state
- uncontrolled recursion
- agent-to-agent improvisation

---

## Artifact-First Architecture

Artifacts are first-class runtime entities.

Examples:

- task plans
- diffs
- logs
- prompts
- reviews
- approvals
- execution metadata
- patches
- test outputs

Conversation history is not the source of truth.

The runtime state is the source of truth.

---

## Replaceable AI Layer

Aeges must not depend on a single AI provider or model.

Supported runners may include:

- Codex CLI
- Claude Code
- Aider
- OpenHands
- local models
- future runners

AI systems are workers behind stable orchestration interfaces.

---

# Architectural Direction

## Runtime Responsibilities

The runtime is responsible for:

- task orchestration
- execution governance
- machine coordination
- artifact persistence
- retries
- rollback
- approvals
- workflow state
- iteration control
- repository isolation

---

## Agent Responsibilities

Agents are responsible for:

- executing assigned tasks
- producing structured artifacts
- following constraints
- operating within isolated workspaces
- reporting deterministic outputs

Agents should not own orchestration decisions.

---

# Task Model

Tasks are durable, auditable units of work.

A task may contain:

- goal
- constraints
- affected paths
- execution status
- approvals
- iteration history
- logs
- diffs
- artifacts
- runtime metadata

Example lifecycle:

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

---

# Worktree Isolation

Each task or iteration should execute inside isolated git worktrees whenever possible.

Goals:

- reproducibility
- rollback safety
- conflict reduction
- deterministic execution
- parallel task support

Example:

```text
project/
project-worktrees/
task-001/
task-002/
```

---

# Locking

Aeges may use lightweight repository locks.

Examples:

- directory locks
- file locks
- dependency locks

Purpose:

- avoid conflicting edits
- support deterministic scheduling
- reduce merge conflicts

Complex semantic locking is not required for MVP.

---

# Repository Intelligence

Repository awareness should remain lightweight initially.

Recommended early capabilities:

- imports
- dependency mapping
- changed-together files
- git history analysis
- lightweight AST inspection
- Tree-sitter based indexing

Avoid early complexity such as:

- full semantic graphs
- heavy indexing infrastructure
- deep code-region scheduling

---

# Multi-Agent Direction

Aeges may support multiple orchestration roles.

Examples:

- Architect
- Executor
- Reviewer
- Tester

However:

- workflows remain runtime-controlled
- loops remain bounded
- approvals remain explicit
- execution remains observable

The runtime always owns lifecycle management.

---

# Remote Runtime Direction

Aeges is designed for remote and distributed execution.

Potential capabilities:

- multi-machine coordination
- remote agent routing
- control-plane orchestration
- durable task synchronization
- offline recovery
- distributed artifact storage

---

# Non-Goals

Aeges is NOT:

- an autonomous AGI system
- uncontrolled recursive agent swarm
- chat-memory-first framework
- prompt-only orchestration layer
- model-specific runtime
- "AI magic"

---

# Philosophy

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