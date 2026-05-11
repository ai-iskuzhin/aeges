# Talk Mode

Talk mode is direct governed discussion with a coding-agent runner.

It is intentionally separate from tasks:

- no project is required
- no task is queued
- no worktree is created
- no task lifecycle state is changed
- no review/approval lifecycle is opened

Talk is still owned by the runtime. Aeges persists durable discussion sessions
and messages in SQLite so the conversation is observable and auditable instead
of becoming an unmanaged side channel.

## Model

Talk uses first-class runtime state:

```text
talk_sessions
talk_messages
```

A talk session stores:

- session ID
- source such as `telegram:<chat-id>` or `cli`
- title
- runner ID
- status
- external runner session ID when the runner provides one
- timestamps

A talk message stores:

- message ID
- session ID
- role: `user`, `assistant`, or `system`
- content
- timestamp

## Telegram

When no task draft or task-continuation draft is active, ordinary Telegram text
is sent to talk mode. This lets an operator write naturally to the bot without
using slash commands.

Telegram replies include compact buttons for:

- `Menu`
- `New task`

The menu remains button-driven. Free text belongs to discussion.

## CLI

CLI talk is explicit:

```text
aeges talk "Can you help me prepare this machine for Aeges?"
```

When no positional message is supplied, `aeges talk` reads stdin.

Useful options:

```text
aeges talk --new "Start a fresh discussion"
aeges talk --session-id <id> "Continue this specific session"
```

## Runner Safety

The initial Codex talk runner uses Codex CLI in read-only sandbox mode and
disables Codex approval prompts. Talk mode is for clarification, diagnostics,
and planning. If real work is needed, the expected next step is to create a
governed task.

Runner artifacts for talk live under:

```text
~/.aeges/artifacts/talk/<session-id>/<message-id>/
```

This keeps runner stdout, stderr, and response files out of target projects.
