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
- organize projects with discovery roots and explicit groups
- talk directly with the configured coding-agent runner without creating a task
- create, inspect, and cancel durable tasks
- run a local agent pass that heartbeats, claims one queued task, creates an
  iteration, writes prompt artifacts, and records runner paths
- optionally create an isolated Git worktree before runner execution
- optionally execute the deterministic mock runner or local Codex CLI runner
- use Telegram long polling with buttons for projects, machines, queued tasks,
  task cancellation, and approval approve/reject actions

The current MVP is intentionally small. There is no native package, web
dashboard, remote control plane, advanced scheduler, or autonomous workflow loop
yet.

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

Install the CLI from the local checkout:

```bash
dotnet pack src/Aeges.Cli/Aeges.Cli.csproj -c Release
AEGES_PACKAGE_SOURCE="$PWD/.artifacts/packages" \
AEGES_VERSION=0.1.0-alpha.6 \
sh scripts/install.sh
```

If your shell cannot find `aeges`, add the .NET tools directory to `PATH`:

```bash
export PATH="$PATH:$HOME/.dotnet/tools"
```

The public shell UX is:

```bash
curl -fsSL https://get.aeges.top/install.sh | sh
wget -qO- https://get.aeges.top/install.sh | sh
```

After the package is published to NuGet, .NET tool installation also works:

```bash
dotnet tool install --global Aeges.Cli --prerelease
```

On Windows, the matching PowerShell UX should be:

```powershell
irm https://get.aeges.top/install.ps1 | iex
```

Versioned GitHub releases are installed by passing `AEGES_VERSION`; the
installer downloads the `.nupkg` and verifies `SHA256SUMS` before installing:

```bash
curl -fsSL https://get.aeges.top/install.sh | AEGES_VERSION=0.1.0-alpha.6 sh
```

```powershell
$env:AEGES_VERSION = "0.1.0-alpha.6"
irm https://get.aeges.top/install.ps1 | iex
```

After Aeges is installed, update the local runtime with:

```bash
aeges update
```

For a specific version or a local development package source:

```bash
aeges update --version 0.1.0-alpha.6
aeges update --version 0.1.0-alpha.6 --package-source "$PWD/.artifacts/packages"
```

`aeges update` downloads release packages through the same GitHub release
artifact contract as the installer, verifies `SHA256SUMS`, and updates the
global .NET tool. Restart background processes after updating so they load the
new runtime:

```bash
aeges agent restart
aeges telegram restart
```

Prepare the local runtime from the project you want Aeges to manage:

```bash
aeges setup
aeges status
aeges version
```

`aeges setup` is the friendly first-use wizard. It creates the local runtime
directories, applies SQLite migrations, registers the current directory as a
project, registers the local machine, can configure Telegram, and can start the
background agent and Telegram transport.

You can still perform those steps explicitly for scripts or operator workflows:

```bash
aeges init
aeges db migrate

aeges project add \
  --project-id aeges \
  --name Aeges \
  --path "$PWD"

aeges machine add \
  --machine-id local \
  --name "$(hostname)" \
  --platform "$(uname -s)"
```

For project discovery, scan a broad folder. Aeges can discover the root, likely
group folders, and projects in one pass:

```bash
aeges root scan "$HOME/work"
aeges root scan "$HOME/work" --apply
```

You can still register roots and groups explicitly when you want stable ids or
scripted setup:

```bash
aeges root add --root-id work --name Work --path "$HOME/work"
aeges group add --group-id analitex --name Analitex --path "$HOME/work/analitex"
```

See [docs/project-organization.md](docs/project-organization.md) for the
recommended `~/work` layout.

Create a governed task:

```bash
aeges task create \
  --project-id aeges \
  --machine-id local \
  --task-id first-task \
  --title "First governed task" \
  --goal "Inspect the repository and report the current implementation status."
```

Run one deterministic agent pass:

```bash
aeges agent run \
  --once \
  --machine-id local
```

That pass claims at most one queued task, moves it into planning, creates the
first iteration, writes a prompt artifact, and records planned artifact/worktree
paths. It does not launch an AI worker unless you explicitly ask it to.

For normal local use, start the background agent worker instead. It keeps
polling the queue and, by default, creates an isolated worktree and executes the
configured runner for assigned tasks:

```bash
aeges agent start
aeges agent restart
aeges agent status
aeges agent stop
```

Agent process metadata is stored in `~/.aeges/runs/agent.pid.json`. Agent logs
are written to `~/.aeges/logs/agent.stdout.log` and
`~/.aeges/logs/agent.stderr.log`.

## Runner Execution

For a safe local dry run, use the mock runner:

```bash
aeges agent run \
  --once \
  --machine-id local \
  --runner-id mock \
  --execute-runner
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
  --execute-runner
```

The default Codex invocation uses Codex JSONL output and a `workspace-write`
sandbox inside the isolated task worktree. Aeges still owns the worktree
boundary and keeps runner output under `~/.aeges/artifacts/`.
For local experiments, Codex sandboxing can be relaxed through config or the
Telegram settings menu. Use that only for trusted repositories because
`danger-full-access` and Codex's bypass flag let the worker operate without
Codex's own sandbox/approval guardrails.

Codex model and reasoning effort can be configured in `~/.aeges/config.json`:

```json
{
  "runners": {
    "default": "codex",
    "codex": {
      "executable": "codex",
      "model": "gpt-5.5",
      "reasoningEffort": "high",
      "sandboxMode": "workspace-write",
      "bypassApprovalsAndSandbox": false,
      "timeoutSeconds": 1800
    }
  }
}
```

## Telegram

Telegram is a transport, not the workflow owner. It calls application services
and uses inline buttons for normal interaction.

Run the setup wizard:

```bash
aeges telegram setup
```

The wizard writes safe Telegram settings to `~/.aeges/config.json`, can store
the bot token in a local secret file under `~/.aeges/secrets/`, and asks for
allowed chat IDs. The token is never written to committed config or logs.

You can also override the local token file with an environment variable when
needed:

```bash
export AEGES_TELEGRAM_BOT_TOKEN="replace-with-your-token"
```

Environment variables are process/session scoped unless you persist them in your
shell profile. For normal local use, the setup wizard's secret file is the more
durable path.

Run the transport:

```bash
aeges telegram check
aeges telegram run
```

`telegram run` stays in the foreground. To keep the Telegram transport running
without occupying the terminal, use the local process wrapper:

```bash
aeges telegram start
aeges telegram restart
aeges telegram status
aeges telegram stop
```

Background process metadata is stored in `~/.aeges/runs/telegram.pid.json`.
Transport logs are written to `~/.aeges/logs/telegram.stdout.log` and
`~/.aeges/logs/telegram.stderr.log`.

Telegram queues tasks; the agent worker processes them. The usual local setup is
therefore:

```bash
aeges telegram start
aeges agent start
```

After changing configuration or installing a new Aeges build, restart both
background processes:

```bash
aeges telegram restart
aeges agent restart
```

Telegram runner settings restart the agent automatically after saving the local
config. Manual restart is still useful after tool updates or direct config file
edits.

Machine status in Telegram comes from the local agent heartbeat. If a machine
shows `offline`, start the agent worker or run a one-shot heartbeat with
`aeges agent run --once --machine-id local --no-claim`.

During local setup, an empty `allowedChatIds` list permits all chats. Before
using a real bot, restrict access in `~/.aeges/config.json`:

```json
{
  "telegram": {
    "botTokenEnvironmentVariable": "AEGES_TELEGRAM_BOT_TOKEN",
    "botTokenFilePath": "/home/user/.aeges/secrets/telegram-bot-token",
    "allowedChatIds": [123456789]
  }
}
```

The MVP Telegram UI includes buttons for:

- creating a new task
- projects, project archive state, and project-scoped task buckets
- machines
- tasks grouped by lifecycle status
- task details
- task cancellation
- pending approvals
- approve/reject approval decisions
- runner settings for Codex sandbox and bypass mode

Telegram keeps button menus compact by using up to two columns.

To run work from Telegram, start both background processes:

```bash
aeges telegram start
aeges agent start
```

Use `aeges telegram restart` and `aeges agent restart` after local tool updates
or configuration changes.

Then send any message to the bot, tap `New task`, choose the project and
machine, then send the title and goal as chat messages. The task is queued
durably and the local agent worker will pick it up on its next poll.

When no task draft is active, ordinary Telegram text goes to talk mode. Talk is
direct discussion with the configured runner, stored as durable Aeges discussion
state, but it does not create a task, choose a project, or create a worktree.
Talk replies include `Menu` and `New task` buttons so you can move from
conversation into governed work.

The `Tasks` button opens status buckets for queued, planning, running,
reviewing, waiting approval, completed, failed, and cancelled tasks. When a
watched task changes, Telegram edits the open task-details message when
possible; otherwise it sends a short notification with a `View task` button.
The `Projects` button lets you pick a project first and then inspect only that
project's queued, running, reviewing, completed, failed, and cancelled work.
Project details include an `Archive` button for active projects. Archiving is
non-destructive: existing tasks, artifacts, and audit history remain visible,
but archived projects are hidden from the `New task` project picker.
Open a task in `reviewing` to see the latest runner response, runner exit
status, and artifacts. Press `Complete` there to confirm the worker result, or
press `Continue` and send follow-up feedback to requeue the task for another
bounded iteration. Completing a task returns Telegram to the completed task
bucket without sending a second completion notification. Telegram starts the
local agent after creating or continuing a task, so queued work can be picked up
without a separate terminal step.
Artifact paths displayed in Telegram point at `~/.aeges/artifacts/...`, not the
target project directory.

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
├── secrets/
└── config.json
```

A safe example config lives at
[`samples/config/local.example.json`](samples/config/local.example.json). When
passing `--config`, use an absolute path.

Useful options:

```text
--config <absolute-path>          Load a specific config file.
--connection-string <value>       Use a specific SQLite database for this command.
--json                            Print deterministic JSON output when supported.
--no-interactive                  Disable prompts for script/CI usage.
```

## CLI Commands

```text
aeges version [--json]
aeges update [--version <version>] [--package-source <path>] [--dry-run] [--json]
aeges setup [--project-id <id>] [--project-name <name>] [--path <path>] [--skip-telegram] [--no-start] [...]
aeges init [--project-id <id>] [--project-name <name>] [--path <path>] [--machine-id <id>] [...]
aeges status [--config <path>] [--connection-string <value>] [--json]
aeges talk [message] [--new] [--session-id <id>] [...]
aeges db status [--config <path>] [--connection-string <value>] [--json]
aeges db migrate [--config <path>] [--connection-string <value>] [--json]
aeges project add --name <name> --path <path> [--project-id <id>] [...]
aeges project list [...]
aeges machine add --name <name> --platform <text> [--machine-id <id>] [...]
aeges machine list [...]
aeges task create --project-id <id> --machine-id <id> --title <title> --goal <goal> [...]
aeges task status <task-id> [...]
aeges task cancel <task-id> [...]
aeges task continue <task-id> --feedback <text> [...]
aeges agent run [--once] [--runner-id <id>] [--create-worktree] [--execute-runner] [...]
aeges agent start [--runner-id <id>] [--no-execute-runner] [--no-create-worktree] [...]
aeges agent restart [--runner-id <id>] [--no-execute-runner] [--no-create-worktree] [...]
aeges agent status [--json]
aeges agent stop [--json]
aeges telegram setup [...]
aeges telegram check [...]
aeges telegram run [--once] [--no-interactive] [--poll-limit <int>] [--timeout-seconds <int>] [...]
aeges telegram start [--poll-limit <int>] [--timeout-seconds <int>] [...]
aeges telegram restart [--poll-limit <int>] [--timeout-seconds <int>] [...]
aeges telegram status [--json]
aeges telegram stop [--json]
```

`aeges version` prints the installed CLI version. The Telegram main menu shows
the same runtime version when the transport is launched through the CLI.

`aeges update` updates the installed global .NET tool from GitHub release
artifacts by default. Use `--dry-run` to inspect the update plan.

`aeges setup` is the recommended first command for local use. `aeges init` and
`aeges db migrate` remain available as lower-level scriptable commands.

`aeges status` is the quickest local health check. It reports the runtime
directory, config path, database migration state, agent and Telegram process
state, Codex CLI availability, project and machine counts, and task counts by
status.

`aeges init` is idempotent. Re-running it keeps existing project and machine
registrations when the selected IDs already exist.

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
- [Install roadmap](docs/install.md)
- [Talk mode](docs/talk.md)
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
