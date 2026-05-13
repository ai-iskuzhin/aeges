# Telegram Transport

Telegram is a transport layer for supervised Aeges operations. It must not own
workflow lifecycle, runner dispatch, approval policy, or domain rules.

The MVP interaction model is button-first for navigation. Free-form inbound
text goes to talk mode when no task draft is active, and all normal navigation
uses inline buttons with stable callback payloads.
Button callbacks edit the existing Telegram message when Telegram provides an
editable message id, so navigation does not spam a chat with repeated menus.
Button menus are laid out as compact grids with at most two columns.

Telegram callback data is limited by the Bot API, while Aeges logical callback
payloads may contain project IDs, task IDs, and status filters. The live
transport rewrites outgoing button callbacks into short durable tokens such as
`a:<token>`, scoped to the originating chat. The original logical callback is
stored in SQLite as a transport callback action and is resolved before the
handler dispatches the action. Legacy direct callback payloads are still
accepted for tests and existing messages.

Initial buttons:

```text
New task      -> ae:t:new
Projects      -> aeges:projects:list
Project       -> ae:p:<project-id>
Archive       -> ae:p:x:<project-id>
Project tasks -> ae:p:<project-id>:s:<status-code>
Machines      -> aeges:machines:list
Tasks        -> ae:t
Task status  -> ae:t:s:<status>
Queued tasks -> aeges:tasks:queued
Back          -> aeges:menu
Task project  -> ae:t:p:<project-id>
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

Only one local Telegram transport should poll a bot at a time. `aeges telegram
run` acquires a local runtime lock under `~/.aeges/runs/telegram.lock`, and
`aeges telegram start` checks that lock before launching a background transport.
This prevents duplicate pollers from sending duplicate responses to the same
operator message.

Allowed chat IDs are enforced before application services are called and act as
an optional hard outer allowlist. When that list is empty, Telegram access is
managed by durable Aeges users instead of being open to everyone. The first chat
that writes to the bot becomes an approved administrator. Later chats are saved
as pending users and receive a waiting message until an administrator approves
or denies them.

Administrators can open `Users` from the main menu, approve pending users, deny
users, and grant access to individual project groups or projects. User access is
sparse by default: no grant means blocked. In the user details screen, red
buttons mean blocked and green buttons mean allowed. Administrators can see all
projects and tasks; non-admin users only see projects they were granted
directly or through a granted project group.

Telegram user records also store the latest observed username, first name, and
last name from inbound messages or button callbacks. These fields are operator
profile metadata used to make approval and access screens recognizable; the
Telegram chat ID remains the routing identifier.

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

Send `/start` to show the main menu.

To create work from Telegram:

1. Send `/start` or use an existing navigation message.
2. Tap `New task`.
3. Choose a project.
4. Choose the machine that should process the task.
5. Send the task title as a message.
6. Send the task goal/details as a message.

The task is created in `queued` state. If the local agent worker is running, it
will claim and process the task on its next polling pass.

In Telegram forum supergroups, General can also act as the task intake lane.
Mention the bot with `new task` or `task:` and an optional topic title:

```text
@aeges_bot new task Fix install docs
@aeges_bot task: Fix install docs
```

When the bot has administrator access with topic management enabled, it creates
a forum topic named from the command and sends the normal task creation wizard
inside that topic. Messages, buttons, task continuation prompts, and watched
task notifications stay scoped to the topic where the task interaction runs.
If topic creation fails, the bot replies in the original chat with the Telegram
permission/API error.

Telegram private chats may also be configured for threaded AI-chatbot mode in
BotFather. Aeges keeps this as an explicit runtime setting because private
threads change routing semantics:

```json
{
  "telegram": {
    "enablePrivateChatThreads": true
  }
}
```

When private chat threads are enabled, Aeges treats
`chat_id + message_thread_id` as the transport surface. This lets one private
conversation with the bot contain multiple task threads. Mention the bot inside
a private thread to start the task wizard in that thread:

```text
@aeges_bot new task Fix install docs
@aeges_bot task: Fix install docs
```

Aeges does not create private Telegram threads itself; the operator creates or
opens the thread in Telegram. The bot only binds work to the `message_thread_id`
Telegram sends. If `enablePrivateChatThreads` is false, private-thread ids are
ignored and private messages continue to route as one normal direct chat.

Task-to-chat/topic bindings are persisted in SQLite. After the Telegram
transport restarts, it reloads those bindings and continues routing task status
updates to the same chat or forum topic.

When no task creation or task-continuation draft is active, ordinary Telegram
text is sent to talk mode. Talk mode creates or continues a durable discussion
session for the chat source, asks the configured runner for a response, and
stores both the operator message and assistant response outside the task
lifecycle. Direct talk responses do not include navigation buttons; use
`/start` when you want to return to the menu.

When private chat threads are enabled, talk sessions are also scoped by private
thread id. The source becomes `telegram:<chat-id>:thread:<message-thread-id>` so
parallel direct-message discussions do not collapse into one talk session.

For inbound text messages, the transport first sends a temporary working
message with a `Cancel` button, then edits that same message with the final
response. This keeps the chat responsive while a runner turn is executing. The
current cancel button is a visible transport affordance; true mid-turn runner
cancellation will require the polling loop to process callback updates while
the original text turn is still running.

The main menu shows count badges for projects, machines, queued tasks, pending
approvals, and users so operators can see queue shape without opening every
view. The projects view lets operators select a project first, then browse that
project's task buckets by lifecycle status. The global task menu still groups
all visible tasks by lifecycle status: queued, planning, running, reviewing,
waiting approval, completed, failed, and cancelled.

Project details include an `Archive` action for active projects. Archiving is a
non-destructive operator action: tasks, artifacts, and history remain visible in
project task buckets, while archived projects are excluded from the `New task`
project picker.

The `reviewing` bucket is the human confirmation step after a worker succeeds.
Opening a reviewing task shows the latest iteration, runner exit status,
registered artifacts, and a short runner response preview. Press `Complete` to
confirm the result, move the task to `completed`, and leave the final task
details message in place without action buttons. Press `Continue` and send
follow-up feedback to store a `review` artifact and requeue the task for another
bounded iteration.

In direct messages, terminal task detail updates keep a `Menu` button so the
operator can return to navigation after a watched task completes, fails, or is
cancelled. In forum topics and private message threads, terminal task detail
messages stay without buttons so the thread can settle as a clean final record.

When Telegram cancels a task, Aeges records a task-scoped runtime event and
closes open iteration or runner execution metadata as cancelled. The task row
remains the source of lifecycle truth, while runtime events explain the
operator action that caused the transition.

For forum-topic tasks, the Telegram transport updates the topic title with the
latest task status, for example `[running] Fix install docs` or `[completed]
Fix install docs`. If Telegram rejects the title update because the bot lacks
topic-management permission, the task flow continues and the failure is logged.

Continuation drafts are scoped to the user who pressed `Continue`. In private
chats, the next non-empty message from that chat is accepted as feedback. In
groups and supergroups, the feedback must come from the same Telegram sender in
the same topic/thread and reply to the bot's follow-up prompt message. This
prevents unrelated group discussion from accidentally becoming task feedback.

Artifact paths shown in Telegram are runtime artifact paths under the local
Aeges artifact root, such as `~/.aeges/artifacts/...`; they are not saved inside
the target project checkout.

When Telegram creates or continues a task, it also runs `aeges agent start
--config <path>`. If the agent is already running, the command leaves it alone;
if it is stopped, queued work can begin processing without a separate terminal
step.

Telegram messages are sent with MarkdownV2 enabled. User/task content such as
goals, approval reasons, failure reasons, and runner response previews is shown
as block quotes so operator-provided text is visually distinct from runtime
metadata.

The main menu includes `Settings`. The settings view exposes agent parallelism,
Codex runner sandbox controls, and private chat thread routing:

- green buttons show currently allowed/enabled behavior
- red buttons show currently disallowed/disabled behavior
- `Parallel tasks: <count>` opens a compact picker for `1`, `2`, `4`, `8`,
  `16`, or `32` project-isolated agent tasks
- `Sandbox enabled` toggles between `workspace-write` and
  `danger-full-access`
- `Bypass disabled` toggles Codex's explicit approvals/sandbox bypass flag
- `Private threads enabled` toggles Telegram private `message_thread_id`
  routing for task drafts, task bindings, watched task details, and talk
  sessions

Settings are written to the local config file. Agent and Codex runner settings
also run `aeges agent restart --config <path>` so the worker process reloads
the updated runner policy. Telegram private thread routing applies immediately
inside the current Telegram transport process because the handler reads the
shared configuration object. If the agent restart command fails, the setting
remains saved and the Telegram settings screen reports the failure.

When a task watched by a chat changes status, the Telegram transport notifies
that chat. If the chat's last bot message is the task details message, the
transport edits that message in place. Otherwise it sends a compact
notification with a `View task` button.

Runner progress events are shown in task details as short telemetry. If a task
details message is currently being watched, new progress events edit that
message in place. Progress-only changes do not send a separate notification
message when the latest bot message is not task details.

The bot token is read from the environment variable named by
`telegram.botTokenEnvironmentVariable`, or from the local file configured by
`telegram.botTokenFilePath`. The token value itself must not be stored in
committed config.

Run the setup wizard to write safe config and store the token in the default
local secret file:

```text
aeges telegram setup
```

The wizard asks once for the bot token, saves it to
`~/.aeges/secrets/telegram-bot-token`, and then asks for allowed chat IDs. If a
secret file already exists, press Enter at the token prompt to keep it.

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
`~/.aeges/logs/telegram.stderr.log`. Startup and non-empty successful polling
batches go to stdout; empty long-poll batches are intentionally not logged.
Recoverable Telegram transport failures, including retryable network or API
request errors, go to stderr. Existing stdout and stderr logs rotate at 5 MB
when the background transport starts, with five historical files retained.
`status` reports whether that pid is still alive, `stop` terminates it and
removes the metadata file, and `restart` stops the recorded process before
starting a fresh transport with the same options accepted by `telegram start`.

For script and CI usage, disable prompts and fail deterministically when required
setup is missing:

```text
aeges telegram run --no-interactive
```

For a bounded poll useful during setup and diagnostics:

```text
aeges telegram run --once --timeout-seconds 5
```
