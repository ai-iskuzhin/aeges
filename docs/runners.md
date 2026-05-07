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
