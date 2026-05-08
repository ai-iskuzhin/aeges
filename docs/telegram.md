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
```

The Telegram handler delegates data access and workflow actions through an
application facade. This keeps Telegram-specific code replaceable and prevents
button callbacks from becoming hidden orchestration logic.

Allowed chat IDs are enforced before application services are called. An empty
allowed-chat list is treated as open local MVP mode; configured chat IDs restrict
the bot to those chats.
