# Task Model

Tasks are durable, auditable units of governed coding work.

Supported lifecycle states:

```text
queued
planning
running
reviewing
waiting_approval
completed
failed
cancelled
```

State transitions must be explicit and validated by the runtime.

The initial core domain model represents task identifiers as strongly typed
value objects and exposes task lifecycle changes through named methods. Direct
state mutation is intentionally avoided so invalid transitions can be rejected
before persistence or runner dispatch.

The application layer exposes these lifecycle changes as task use cases. It
loads the task from storage, delegates transition validation to the core domain
model, persists only accepted transitions, and returns structured expected
failures for missing tasks, invalid transitions, or exceeded iteration limits.

Initial allowed transitions:

```text
queued -> planning
queued -> cancelled
planning -> running | waiting_approval | failed | cancelled
running -> reviewing | waiting_approval | failed | cancelled
reviewing -> completed | running | waiting_approval | failed | cancelled
waiting_approval -> running | failed | cancelled
```

Terminal states do not allow further transitions.

## Iterations

A task may have multiple bounded iterations. Each iteration should record the
runner, prompt artifact, stdout, stderr, diff, result, review, execution
metadata, status, and timestamps.

Initial iteration states:

```text
created
running
reviewing
waiting_approval
completed
failed
cancelled
```

Initial allowed iteration transitions:

```text
created -> running
created -> cancelled
running -> reviewing | waiting_approval | failed | cancelled
reviewing -> completed | running | waiting_approval | failed | cancelled
waiting_approval -> running | failed | cancelled
```

Terminal iteration states do not allow further transitions.

## Artifacts

Artifacts are durable metadata records for files stored outside the database.
SQLite should store identifiers, ownership, type, relative path, size, checksum,
and timestamps. Large artifact contents remain file-based.

Initial artifact types:

```text
prompt
plan
stdout_log
stderr_log
result
diff
review
approval_request
test_output
metadata
```

Artifact paths must be relative to the configured artifact root and must not use
absolute paths or parent-directory traversal.

## Approvals

Approvals are durable governance gates attached to a task and optionally to an
iteration.

Initial approval states:

```text
pending
approved
rejected
cancelled
```

Only pending approvals can be resolved. Approved, rejected, and cancelled
approvals are terminal.

The application layer treats approval as a workflow gate, not only an approval
record. Requesting approval pauses the owning task in `waiting_approval`.
Resolving approval applies a deterministic task outcome:

```text
approved  -> task resumes running
rejected  -> task fails with a recorded reason
cancelled -> task is cancelled
```

Conversation history is not the source of truth. Durable artifacts are.
