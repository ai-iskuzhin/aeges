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
variables, policy hints, and an explicit runner session policy. Session
continuity is runner metadata; task lifecycle and artifacts remain owned by
Aeges.

Runner results include status, exit code, stdout/stderr paths, result artifact
path, produced artifact paths, and an error summary when execution does not
succeed.

Aeges stores every launched worker process as a durable runner execution record.
The record captures the owning task and iteration, runner id, command
description, working directory, start and completion timestamps, exit code, and
whether the runtime ended execution because of timeout or cancellation.

The application layer owns the use case for starting and completing these
records. Agents and transports should call that use case instead of writing
runner execution rows directly.

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

`Aeges.Runners.Codex` builds Codex CLI command descriptions from governed
runner requests and can execute them through a local process shell. It records
the executable, arguments, working directory, timeout, and environment
variables, captures stdout and stderr into artifact files, maps process exit
codes into runner results, and reports timeout or cancellation without mutating
task lifecycle state directly.

Before building a command, the Codex runner preflights the configured executable
name or path. If Codex is not available on the machine, Aeges fails before
dispatch and reports the Codex project URL:

```text
https://github.com/openai/codex
```

The Codex runner can also pin the model and reasoning effort. The model is
passed with Codex CLI's `--model` option. Reasoning effort is passed through the
Codex configuration override mechanism:

```text
codex exec --model gpt-5.5 --config model_reasoning_effort="high" <prompt>
```

Local configuration can set:

```json
{
  "runners": {
    "codex": {
      "model": "gpt-5.5",
      "reasoningEffort": "high"
    }
  }
}
```

Codex JSON mode emits a thread identifier when a new session starts:

```json
{"type":"thread.started","thread_id":"019e05e0-d00b-7182-8516-0d258c7993aa"}
```

Aeges can store this identifier as external runner session metadata. When a
later governed execution explicitly resumes that session, the Codex command
builder emits:

```text
codex exec resume 019e05e0-d00b-7182-8516-0d258c7993aa <prompt>
```

The runtime must not resume the most recent Codex session implicitly. Hidden
worker memory is useful continuity, but Aeges should only resume a session when
the durable task or iteration explicitly references that external session id.

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
