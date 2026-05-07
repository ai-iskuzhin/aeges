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
```

The runtime should validate paths, preserve artifacts, and avoid modifying files
outside approved project roots.
