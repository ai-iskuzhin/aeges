# Telegram Transport

Telegram is a transport layer for supervised Aeges operations. It must not own
workflow lifecycle, runner dispatch, approval policy, or domain rules.

The MVP interaction model is button-first. Inbound text opens the main menu, and
all normal navigation uses inline buttons with stable callback payloads.
Button callbacks edit the existing Telegram message when Telegram provides an
editable message id, so navigation does not spam a chat with repeated menus.

Initial buttons:

```text
New task      -> ae:t:new
Projects      -> aeges:projects:list
Machines      -> aeges:machines:list
Tasks        -> ae:t
Task status  -> ae:t:s:<status>
Queued tasks -> aeges:tasks:queued
Back          -> aeges:menu
Project       -> ae:t:p:<project-id>
Machine       -> ae:t:m:<machine-id>
Cancel draft  -> ae:t:new:x
Task details  -> aeges:task:<task-id>
Cancel task   -> ae:t:x:<task-id>
Approvals     -> ae:ap
Approval      -> ae:a:<approval-id>
Approve       -> ae:a:y:<approval-id>
Reject        -> ae:a:n:<approval-id>
```

The Telegram handler delegates data access and workflow actions through an
application facade. This keeps Telegram-specific code replaceable and prevents
button callbacks from becoming hidden orchestration logic.

Approval buttons resolve through the application approval service. The Telegram
chat ID is recorded as the resolver in the form `telegram:<chat-id>`.

The live transport uses long polling for the local-first MVP. The polling loop
fetches message and callback-query updates, acknowledges callback queries, then
sends the handler response as a Telegram message with inline keyboard markup or
edits the callback message in place.

Allowed chat IDs are enforced before application services are called. An empty
allowed-chat list is treated as open local MVP mode; configured chat IDs restrict
the bot to those chats.

Machine status in Telegram reflects the local Aeges agent heartbeat, not whether
the Telegram transport is running. A newly registered machine starts as
`offline`; `aeges agent run --once --machine-id <id>` records a heartbeat and
marks it `online`.

Telegram does not execute queued tasks. It is the input and approval transport.
The local agent worker owns queue processing and runner dispatch. For automatic
local processing, run both:

```text
aeges telegram start
aeges agent start
```

To create work from Telegram:

1. Open the bot menu by sending any message.
2. Tap `New task`.
3. Choose a project.
4. Choose the machine that should process the task.
5. Send the task title as a message.
6. Send the task goal/details as a message.

The task is created in `queued` state. If the local agent worker is running, it
will claim and process the task on its next polling pass.

The main menu shows count badges for projects, machines, queued tasks, and
pending approvals so operators can see queue shape without opening every view.
The task menu groups tasks by lifecycle status: queued, planning, running,
reviewing, waiting approval, completed, failed, and cancelled.

The `reviewing` bucket is the human confirmation step after a worker succeeds.
Opening a reviewing task shows the latest iteration, runner exit status,
registered artifacts, and a short runner response preview. Press `Complete` to
confirm the result and move the task to `completed`.

Telegram messages are sent with MarkdownV2 enabled. User/task content such as
goals, approval reasons, failure reasons, and runner response previews is shown
as block quotes so operator-provided text is visually distinct from runtime
metadata.

The main menu includes `Settings`. The settings view exposes Codex runner
sandbox controls:

- green buttons show currently allowed/enabled behavior
- red buttons show currently disallowed/disabled behavior
- `Sandbox enabled` toggles between `workspace-write` and
  `danger-full-access`
- `Bypass disallowed` toggles Codex's explicit approvals/sandbox bypass flag

Settings are written to the local config file. When a setting changes, the
Telegram transport runs `aeges agent restart --config <path>` so the worker
process reloads the updated runner policy. If the restart command fails, the
setting remains saved and the Telegram settings screen reports the failure.

When a task watched by a chat changes status, the Telegram transport notifies
that chat. If the chat's last bot message is the task details message, the
transport edits that message in place. Otherwise it sends a compact
notification with a `View task` button.

The bot token is read from the environment variable named by
`telegram.botTokenEnvironmentVariable`, or from the local file configured by
`telegram.botTokenFilePath`. The token value itself must not be stored in
committed config.

Run the setup wizard to write safe config and store the token in a local secret
file:

```text
aeges telegram setup
```

If no token source is configured, `aeges telegram run` prompts for a token and
loads it into the current process only. Prompted tokens are not written to config
or logs. Environment variables are useful for automation, but the local secret
file is the durable default for one-machine development.

Run the local Telegram transport with:

```text
aeges telegram check
aeges telegram run
```

`aeges telegram check` calls Telegram `getMe` and prints the configured bot
identity. It is the quickest token/configuration test before long polling.

`aeges telegram run` is a foreground command. For normal local use, the CLI also
provides a small process wrapper:

```text
aeges telegram start
aeges telegram restart
aeges telegram status
aeges telegram stop
```

`start` launches `telegram run --no-interactive` in the background, writes
metadata to `~/.aeges/runs/telegram.pid.json`, and redirects output to
`~/.aeges/logs/telegram.stdout.log` and
`~/.aeges/logs/telegram.stderr.log`. `status` reports whether that pid is still
alive, `stop` terminates it and removes the metadata file, and `restart` stops
the recorded process before starting a fresh transport with the same options
accepted by `telegram start`.

For script and CI usage, disable prompts and fail deterministically when required
setup is missing:

```text
aeges telegram run --no-interactive
```

For a bounded poll useful during setup and diagnostics:

```text
aeges telegram run --once --timeout-seconds 5
```
