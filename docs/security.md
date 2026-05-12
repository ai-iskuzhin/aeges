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

The local runtime configuration model stores secret references, not secret
values. For example, Telegram configuration stores the environment variable name
that contains the bot token or the local token file path. `aeges telegram setup`
may store the bot token in a local secret file under `~/.aeges/secrets/`. This
file is outside the repository, must not be committed, and is created with
user-only permissions on Unix-like systems.

Telegram operator access is durable runtime state. If no outer Telegram
allowlist is configured, the first chat that writes to the bot is bootstrapped
as an approved administrator and later chats are created as pending users.
Administrators can approve or deny users and grant access to project groups or
individual projects. Non-admin users have no project access until a grant exists.
If `telegram.allowedChatIds` is configured, it remains a hard outer allowlist
before durable user records are created or read.

Approval should be required for destructive git commands, dependency changes,
database migrations, file deletion, deployment changes, CI/CD modifications, and
force push operations.

Codex runner sandbox settings are local machine policy. The default is
`workspace-write` inside an isolated task worktree. Operators can set
`runners.codex.sandboxMode` to `danger-full-access` or enable
`runners.codex.bypassApprovalsAndSandbox` for trusted local experiments, but
those settings remove Codex's own sandbox/approval guardrails. Aeges still
records runner execution metadata, artifacts, and review state, but the machine
owner is responsible for when those Codex-level protections are disabled.

## Governance Policy

The initial runtime governance policy is evaluated before runner dispatch. It
can reject execution when hard boundaries are crossed, require approval for
sensitive paths or commands, or allow dispatch when checks pass.

Implemented MVP checks:

- maximum iteration count
- maximum runner timeout
- allowed path patterns
- denied path patterns
- approval-required path patterns for dependency, migration, CI/CD, and
  deployment-related files
- approval-required command fragments for destructive or publishing-oriented git
  operations

Rejected decisions take priority over approval-required decisions. This keeps
hard safety boundaries deterministic even when a requested change would also
normally require approval.

## Path Locks

MVP locking is path-based and relative to a registered project root. Lock
patterns must not be absolute and must not contain parent-directory traversal.
This keeps early scheduling deterministic without introducing semantic
code-region locking before the runtime needs it.

The application layer now acquires locks through a governed use case. Before a
lock is persisted, active locks for the project are checked for path overlap.
Overlapping active locks held by another task are rejected as `lock_conflict`;
overlapping locks held by the same task are allowed so one task can reserve a
broader area and a specific file inside it. Released locks no longer participate
in conflict detection.
