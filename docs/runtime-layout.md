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
