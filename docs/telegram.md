# Telegram Transport

Telegram is a transport layer for supervised Aeges operations. It must not own
workflow lifecycle, runner dispatch, approval policy, or domain rules.

The MVP interaction model is button-first. Inbound text opens the main menu, and
all normal navigation uses inline buttons with stable callback payloads.

Initial buttons:

```text
Projects      -> aeges:projects:list
Machines      -> aeges:machines:list
Queued tasks  -> aeges:tasks:queued
Back          -> aeges:menu
Task details  -> aeges:task:<task-id>
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
sends the handler response as a Telegram message with inline keyboard markup.

Allowed chat IDs are enforced before application services are called. An empty
allowed-chat list is treated as open local MVP mode; configured chat IDs restrict
the bot to those chats.

The bot token is read from the environment variable named by
`telegram.botTokenEnvironmentVariable`; the token value itself must not be
stored in committed config.

Run the local Telegram transport with:

```text
aeges telegram run
```

For a bounded poll useful during setup and diagnostics:

```text
aeges telegram run --once --timeout-seconds 5
```
