# Aeges

**Reliable Governed Coding Runtime**

Aeges is a local-first runtime for supervised AI-assisted software development.
It is not an autonomous coding agent and it is not a chat wrapper. Aeges owns
task lifecycle, durable state, approvals, artifacts, runner dispatch, and
governance. Coding tools such as Codex CLI are replaceable workers.

```text
LLMs are workers.
Aeges owns execution.
```

## Is It Usable?

Yes, as an MVP for local governed execution. Today you can:

- create and migrate the local SQLite database
- register projects and machines
- create, inspect, and cancel durable tasks
- run a local agent pass that heartbeats, claims one queued task, creates an
  iteration, writes prompt artifacts, and records runner paths
- optionally create an isolated Git worktree before runner execution
- optionally execute the deterministic mock runner or local Codex CLI runner
- use Telegram long polling with buttons for projects, machines, queued tasks,
  task cancellation, and approval approve/reject actions

The current MVP is intentionally small. There is no installer, web dashboard,
remote control plane, advanced scheduler, or autonomous workflow loop yet.

## Requirements

- .NET 10 SDK
- Git
- SQLite support through EF Core packages restored by `dotnet restore`
- Optional: Codex CLI for real Codex worker execution:
  <https://github.com/openai/codex>
- Optional: a Telegram bot token for the Telegram transport

## First Run

From the repository root:

```bash
dotnet restore
dotnet build --no-restore
dotnet test --no-build
```

For convenience while developing from source, define a shell function:

```bash
aeges() { dotnet run --project src/Aeges.Cli -- "$@"; }
```

Use a local throwaway database inside the ignored `.aeges/` directory:

```bash
mkdir -p .aeges
export AEGES_DB="Data Source=$PWD/.aeges/aeges.db"
```

Create the schema and check migration status:

```bash
aeges db migrate --connection-string "$AEGES_DB"
aeges db status --connection-string "$AEGES_DB"
```

Register this repository as a project and this machine as an executor:

```bash
aeges project add \
  --project-id aeges \
  --name Aeges \
  --path "$PWD" \
  --connection-string "$AEGES_DB"

aeges machine add \
  --machine-id local \
  --name "$(hostname)" \
  --platform "$(uname -s)" \
  --connection-string "$AEGES_DB"
```

Create a governed task:

```bash
aeges task create \
  --project-id aeges \
  --machine-id local \
  --task-id first-task \
  --title "First governed task" \
  --goal "Inspect the repository and report the current implementation status." \
  --connection-string "$AEGES_DB"
```

Run one deterministic agent pass:

```bash
aeges agent run \
  --once \
  --machine-id local \
  --connection-string "$AEGES_DB"
```

That pass claims at most one queued task, moves it into planning, creates the
first iteration, writes a prompt artifact, and records planned artifact/worktree
paths. It does not launch an AI worker unless you explicitly ask it to.

## Runner Execution

For a safe local dry run, use the mock runner:

```bash
aeges agent run \
  --once \
  --machine-id local \
  --runner-id mock \
  --execute-runner \
  --connection-string "$AEGES_DB"
```

For Codex, install and authenticate Codex CLI first. Aeges checks that the
configured executable exists before dispatch and reports the Codex project URL
when it is missing.

Codex execution is gated behind Git worktree creation so it does not run inside
the source checkout:

```bash
aeges agent run \
  --once \
  --machine-id local \
  --runner-id codex \
  --create-worktree \
  --execute-runner \
  --connection-string "$AEGES_DB"
```

Codex model and reasoning effort can be configured in `~/.aeges/config.json`:

```json
{
  "runners": {
    "default": "codex",
    "codex": {
      "executable": "codex",
      "model": "gpt-5.5",
      "reasoningEffort": "high",
      "timeoutSeconds": 1800
    }
  }
}
```

## Telegram

Telegram is a transport, not the workflow owner. It calls application services
and uses inline buttons for normal interaction.

Set a bot token in the environment:

```bash
export AEGES_TELEGRAM_BOT_TOKEN="replace-with-your-token"
```

Run the transport:

```bash
aeges telegram run --connection-string "$AEGES_DB"
```

During local setup, an empty `allowedChatIds` list permits all chats. Before
using a real bot, restrict access in `~/.aeges/config.json`:

```json
{
  "telegram": {
    "botTokenEnvironmentVariable": "AEGES_TELEGRAM_BOT_TOKEN",
    "allowedChatIds": [123456789]
  }
}
```

The MVP Telegram UI includes buttons for:

- projects
- machines
- queued tasks
- task details
- task cancellation
- pending approvals
- approve/reject approval decisions

## Configuration

By default Aeges reads optional configuration from:

```text
~/.aeges/config.json
```

If no storage connection string is configured, the default database path is:

```text
~/.aeges/aeges.db
```

The runtime layout is:

```text
~/.aeges/
├── aeges.db
├── logs/
├── runs/
├── worktrees/
├── artifacts/
└── config.json
```

A safe example config lives at
[`samples/config/local.example.json`](samples/config/local.example.json). When
passing `--config`, use an absolute path.

## CLI Commands

```text
aeges db status [--config <path>] [--connection-string <value>] [--json]
aeges db migrate [--config <path>] [--connection-string <value>] [--json]
aeges project add --name <name> --path <path> [--project-id <id>] [...]
aeges project list [...]
aeges machine add --name <name> --platform <text> [--machine-id <id>] [...]
aeges machine list [...]
aeges task create --project-id <id> --machine-id <id> --title <title> --goal <goal> [...]
aeges task status <task-id> [...]
aeges task cancel <task-id> [...]
aeges agent run [--once] [--runner-id <id>] [--create-worktree] [--execute-runner] [...]
aeges telegram run [--once] [--poll-limit <int>] [--timeout-seconds <int>] [...]
```

Most commands support `--json` for deterministic machine-readable output.

## Architecture

```text
src/Aeges.Core              Domain model, identifiers, lifecycle rules
src/Aeges.Application       Use cases, governance, orchestration boundaries
src/Aeges.Storage           Storage contracts
src/Aeges.Storage.Sqlite    EF Core SQLite persistence and migrations
src/Aeges.Runners           Runner contracts and mock runner
src/Aeges.Runners.Codex     Codex CLI runner implementation
src/Aeges.Git               Git repository and worktree integration
src/Aeges.Agent             Local runtime host
src/Aeges.Cli               Operator/developer CLI
src/Aeges.Telegram          Telegram button transport
```

Dependency direction is deliberate: core stays infrastructure-free, application
coordinates through interfaces, and transports do not own domain rules.

## Development

Run the CI-equivalent loop locally:

```bash
dotnet restore
dotnet build --no-restore
dotnet test --no-build
```

SQLite schema changes are EF Core migrations owned by
`src/Aeges.Storage.Sqlite`. Use standard `dotnet ef migrations ...` commands
against that project when changing the persistence model.

## Security

Never commit real bot tokens, API keys, Codex credentials, SSH keys,
machine-specific config, or local runtime databases. Secrets should come from
environment variables or future OS secret-store integration. The committed
sample config names secret environment variables; it does not contain secret
values.

## Docs

- [Architecture](docs/architecture.md)
- [Runtime layout](docs/runtime-layout.md)
- [SQLite storage](docs/sqlite.md)
- [Task model](docs/task-model.md)
- [Runners](docs/runners.md)
- [Telegram transport](docs/telegram.md)
- [Security](docs/security.md)
- [MVP checklist](docs/checklist.md)

## Non-Goals

Aeges is not:

- an autonomous AI swarm
- a model-specific agent framework
- a prompt-memory system
- a chat wrapper
- a replacement for Git or CI

The goal is reliable governed execution infrastructure.
