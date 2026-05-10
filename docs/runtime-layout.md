# Runtime Layout

The default local runtime directory is:

```text
~/.aeges/
```

Recommended layout:

```text
~/.aeges/
├── aeges.db
├── logs/
├── runs/
├── worktrees/
├── artifacts/
├── secrets/
└── config.json
```

`Aeges.Application` exposes this as a runtime directory layout model. The model
normalizes the root path, requires it to be absolute, exposes the database,
logs, runs, worktrees, artifacts, secrets, and config paths, and lists the
directories that `aeges init` should create.

Local configuration is loaded from the runtime `config.json`, environment
variables with the `AEGES_` prefix, and command-line arguments. Later sources
override earlier sources. Config stores references to secrets, such as the
Telegram bot token environment variable name or local token file path, rather
than storing secret values directly.

The CLI can start the local agent shell with:

```text
aeges agent run
```

The current shell initializes the local SQLite database, records a heartbeat for
the configured machine, reports a bounded queued-task snapshot, and claims at
most one queued task assigned to that machine. A claim moves the task into
planning, creates its next bounded iteration, writes a prompt file under
`artifacts/`, registers prompt artifact metadata, and records the planned
worktree path on the iteration. Use `--once` for a single deterministic pass,
and `--no-claim` when only a heartbeat and queue preview are needed.

Runner execution is explicit. Use `--execute-runner` to execute the prepared
runner request. The deterministic mock runner is useful for local dry runs and
tests because it does not require real AI credentials:

```text
aeges agent run --once --runner-id mock --execute-runner
```

Git worktree creation is also explicit:

```text
aeges agent run --once --create-worktree
```

Codex execution is available only when worktree creation is enabled, so Codex is
not launched against the source checkout:

```text
aeges agent run --once --runner-id codex --create-worktree --execute-runner
```

For normal local background operation, use:

```text
aeges agent start
aeges agent restart
aeges agent status
aeges agent stop
```

`agent start` launches the continuous `agent run` worker as a detached local
process. By default it enables worktree creation and runner execution so tasks
queued from Telegram can be processed automatically. Process metadata is stored
in `~/.aeges/runs/agent.pid.json`, and output is redirected to
`~/.aeges/logs/agent.stdout.log` and `~/.aeges/logs/agent.stderr.log`.
`agent restart` stops the recorded worker when it is running, clears stale
metadata when needed, and starts a fresh worker with the same options accepted
by `agent start`.

The runner id for newly created iterations defaults to configured
`runners.default` and can be overridden with:

```text
aeges agent run --runner-id codex
```

Git worktrees should be isolated per task or iteration:

```text
~/.aeges/worktrees/
  <project-id>/
    <task-id>/
      <iteration-id>/
```

The Git foundation includes deterministic worktree path generation under the
configured worktree root. Identifier values are converted into safe path
segments, generated worktree paths are kept inside the root, and candidate
worktree paths can be validated before use. A process-backed Git runtime can
detect repositories, capture status and diffs, record base commits, and create
isolated worktrees with `git worktree add`.

The runtime should preserve artifacts and avoid modifying files outside
approved project roots.
