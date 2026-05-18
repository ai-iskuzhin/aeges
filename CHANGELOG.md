# Changelog

All notable changes to Aeges are documented in this file.

## [Unreleased]

### Added

- Nothing yet.

## [0.1.0-alpha.9] - 2026-05-18

### Changed

- Raised the default task iteration limit to 10 for new tasks and governance
  defaults.
- Simplified Telegram task details by moving result, progress, and artifact
  content into task subviews.
- Made continued task prompts prioritize the latest follow-up instruction while
  keeping the original goal and older follow-ups as context.

### Fixed

- Allowed cancelled tasks to be continued with a follow-up instruction.
- Showed the latest follow-up instruction in Telegram task details.
- Opened task details directly after submitting final task or follow-up
  instructions.

## [0.1.0-alpha.8] - 2026-05-14

### Added

- Added Telegram task topic bindings, private task thread routing, and project
  visibility controls for multi-user Telegram operation.
- Added durable runner progress events and streamed Telegram progress drafts
  while Codex work is running.
- Added bounded runtime log rotation for foreground and background process logs.

### Changed

- Detached Unix background agent and Telegram processes so CLI startup returns
  cleanly while child processes keep running.
- Improved Telegram project navigation with full-width project buttons and more
  compact task controls.

### Fixed

- Recovered orphaned active tasks on agent startup after process restarts.
- Hardened Telegram polling around stale callback acknowledgements, chat
  migrations, and transient Bot API transport failures.

## [0.1.0-alpha.7] - 2026-05-12

### Added

- Added durable Telegram user management with first-user admin bootstrap,
  pending user approval, denial, and per-project/per-group access grants.
- Added Telegram access controls so non-admin users only see explicitly granted
  projects and tasks.
- Added runtime logs for foreground and background agent/Telegram processes.

### Changed

- Simplified `aeges telegram setup` so it asks for the bot token once and stores
  it in the local secret file by default.
- `aeges update` now prints progress before release resolution/download work.

### Fixed

- `aeges update` now skips the tool update when the resolved target version
  already matches the installed CLI version.

## [0.1.0-alpha.6] - 2026-05-12

### Fixed

- Pinned public installer defaults to the current alpha release so
  `get.aeges.top` does not resolve through GitHub's non-prerelease `latest`
  shortcut.
- Updated `aeges update` to resolve the newest GitHub release that contains an
  Aeges CLI package, including prerelease tags.

## [0.1.0-alpha.5] - 2026-05-12

### Added

- Added project discovery roots and explicit project groups.
- Added `aeges root scan <path-or-root-id>` with dry-run discovery and
  `--apply` persistence for roots, inferred groups, and projects.
- Added Telegram project grouping so group buttons open project-scoped lists.
- Added a project-scoped `New task` button on Telegram project details.

### Changed

- `aeges root scan ~/work` now treats an existing directory as a discovery
  target instead of requiring a pre-registered root id.
- Telegram project details now show the project group.
- Telegram project archive now requires explicit confirmation before mutating
  state.

## [0.1.0-alpha.4] - 2026-05-12

### Added

- Added `aeges setup` as a friendly first-use wizard over runtime initialization,
  SQLite migrations, Telegram setup, and optional background process start.
- Added `aeges version` and version display in local status and Telegram menus.
- Added `aeges update` for updating the installed runtime from GitHub release
  artifacts or a local package source.
- Added NuGet publishing support to the release workflow through `NUGET_TOKEN`
  in the `production` GitHub environment.

### Changed

- Documented NuGet tool installation and update paths alongside the shell
  installer flow.

## [0.1.0-alpha.3] - 2026-05-12

### Fixed

- Fixed PowerShell installer interpolation for package checksum success output.
- Added a regression test for ambiguous unscoped PowerShell variable interpolation.

## [0.1.0-alpha.2] - 2026-05-12

### Added

- Durable Telegram callback tokens backed by SQLite to keep inline button data within Telegram limits.
- Governed direct talk mode through CLI and Telegram without creating a task.
- Telegram pending text responses that are edited in place when the runner response is ready.
- `/start` command support for opening the Telegram navigation menu.
- Local Telegram runtime lock to prevent duplicate pollers from processing the same bot updates.
- Codex talk runner support for non-repository runtime directories.

### Changed

- Direct Telegram talk responses are cleaner and no longer include navigation buttons.
- Telegram talk metadata is shown as a compact session/runner quote above the response.

### Fixed

- Telegram long polling now survives transient Telegram API transport failures.
- Codex talk mode no longer fails only because the runtime directory is outside a trusted Git repository.

## [0.1.0-alpha.1] - 2026-05-11

### Added

- Initial local-first governed runtime skeleton.
- Durable SQLite storage with EF Core migrations.
- Core task, iteration, artifact, approval, machine, project, lock, and runner execution models.
- CLI setup and runtime commands for database, projects, tasks, agent, and Telegram.
- Local agent runtime with mock and Codex runner support.
- Git worktree isolation and artifact capture.
- Button-first Telegram UI for projects, tasks, approvals, settings, and task continuation.
- Public shell and PowerShell installers through `get.aeges.top`.
- GitHub release assets for the CLI package, installers, and checksums.
