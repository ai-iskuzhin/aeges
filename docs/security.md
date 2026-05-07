# Security

Aeges is governance-first infrastructure. Execution should be observable,
auditable, interruptible, reproducible, reviewable, and bounded.

Never commit:

- bot tokens
- API keys
- local runtime databases
- SSH keys
- machine-specific configs
- real credentials

Secrets should come from environment variables, local untracked configuration,
or a future OS secret store integration.

Approval should be required for destructive git commands, dependency changes,
database migrations, file deletion, deployment changes, CI/CD modifications, and
force push operations.

## Path Locks

MVP locking is path-based and relative to a registered project root. Lock
patterns must not be absolute and must not contain parent-directory traversal.
This keeps early scheduling deterministic without introducing semantic
code-region locking before the runtime needs it.
