# Changelog

All notable changes to Aeges are documented in this file.

## [Unreleased]

### Added

- Nothing yet.

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
