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
└── config.json
```

`Aeges.Application` exposes this as a runtime directory layout model. The model
normalizes the root path, requires it to be absolute, exposes the database,
logs, runs, worktrees, artifacts, and config paths, and lists the directories
that `aeges init` should create.

Local configuration is loaded from the runtime `config.json`, environment
variables with the `AEGES_` prefix, and command-line arguments. Later sources
override earlier sources. Config stores references to secrets, such as the
Telegram bot token environment variable name, rather than storing secret values.

The CLI can start the local agent shell with:

```text
aeges agent run
```

The current shell initializes the local SQLite database, records a heartbeat for
the configured machine, and reports a bounded queued-task snapshot. Use
`--once` for a single deterministic heartbeat, which is useful for setup checks
and scripts.

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
worktree paths can be validated before use.

The runtime should preserve artifacts and avoid modifying files outside
approved project roots.
