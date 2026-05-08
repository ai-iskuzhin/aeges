# Runners

Runners are replaceable workers. Aeges owns orchestration, lifecycle, approvals,
artifacts, cancellation, retries, and durable state.

The shared runner contract lives in `Aeges.Runners` and depends only on core
runtime identifiers.

## Contract

Runner implementations expose:

- a stable `RunnerId`
- a cancellation-aware `RunAsync` method
- a `RunnerRequest` describing the governed execution context
- a `RunnerResult` describing durable result metadata

Runner requests include task, iteration, and project identifiers, project and
worktree paths, prompt path, artifact output directory, timeout, environment
variables, and policy hints.

Runner results include status, exit code, stdout/stderr paths, result artifact
path, produced artifact paths, and an error summary when execution does not
succeed.

## Dispatch Governance

Before a runner is dispatched, the application layer evaluates the dispatch
against the active governance policy. The MVP policy can allow dispatch, require
an approval checkpoint, or reject dispatch when hard limits are exceeded. Runner
implementations receive policy hints, but they do not own the governance
decision.

## Mock Runner

`Aeges.Runners` includes a deterministic mock runner for tests and local dry
runs. It can report success, failure, timeout, cancellation, or approval
required without contacting a real AI backend.

## Codex Contract

`Aeges.Runners.Codex` currently builds Codex CLI command descriptions from
governed runner requests. It records the executable, arguments, working
directory, timeout, and environment variables but does not launch Codex yet.
Process execution, stdout/stderr capture, timeout enforcement, and artifact
writing are intentionally left for a later implementation slice.

Before building a command, the Codex runner preflights the configured executable
name or path. If Codex is not available on the machine, Aeges fails before
dispatch and reports the Codex project URL:

```text
https://github.com/openai/codex
```

## Statuses

Initial runner statuses:

```text
succeeded
failed
timed_out
cancelled
approval_required
```

Runner implementations must not decide task lifecycle transitions directly.
They report results; the application/runtime layer decides what state changes
follow.
