# Installation

Aeges should be easy to install without hiding how the runtime works. The
installation path should mature in layers, starting with the native .NET tool
flow and expanding into platform package managers when releases are stable.

## Recommended Shell UX

The friendly macOS/Linux install path should feel like Docker-style installers:

```bash
curl -fsSL https://aeges.dev/install.sh | sh
```

Equivalent forms should also work:

```bash
wget -qO- https://aeges.dev/install.sh | sh
```

```bash
curl -fsSL https://aeges.dev/install.sh -o install.sh
sh install.sh
```

Until `aeges.dev` and release artifacts exist, use the checked-in installer
from a local checkout:

```bash
dotnet pack src/Aeges.Cli/Aeges.Cli.csproj -c Release
AEGES_PACKAGE_SOURCE="$PWD/.artifacts/packages" \
AEGES_VERSION=0.1.0-alpha.1 \
sh scripts/install.sh
```

The script installs or updates the CLI as a global .NET tool. It is intentionally
thin: it does not install Codex, Telegram, background services, or machine-local
configuration. Those remain explicit runtime setup steps.

Supported installer environment variables:

```text
AEGES_VERSION=0.1.0-alpha.1
AEGES_PACKAGE_SOURCE=/path/to/packages
AEGES_TOOL_PACKAGE=Aeges.Cli
AEGES_TOOL_COMMAND=aeges
```

The script requires the .NET SDK to already be available on `PATH`.

## .NET Tool

The CLI is packaged as a .NET tool. From a local checkout:

```bash
dotnet pack src/Aeges.Cli/Aeges.Cli.csproj -c Release
dotnet tool install --global Aeges.Cli \
  --add-source "$PWD/.artifacts/packages" \
  --version 0.1.0-alpha.1
```

If `aeges` is still not found, add the .NET tool directory to `PATH`:

```bash
export PATH="$PATH:$HOME/.dotnet/tools"
```

For repeated local installs during development:

```bash
dotnet pack src/Aeges.Cli/Aeges.Cli.csproj -c Release
AEGES_PACKAGE_SOURCE="$PWD/.artifacts/packages" \
AEGES_VERSION=0.1.0-alpha.1 \
sh scripts/install.sh
```

## First Public Release: NuGet Tool

When Aeges is ready for public package publishing, the primary cross-platform
install command should become:

```bash
dotnet tool install --global Aeges.Cli
```

This keeps the first public distribution simple and works consistently on
Windows, macOS, and Linux for users who already have the .NET SDK.

## Release Shell Installer

The release shell installer should keep the same `curl | sh` UX while adding
release hardening:

- download a versioned release artifact
- verify a checksum
- install without `sudo` by default
- add clear PATH guidance
- support explicit version selection
- provide idempotent reinstall/update behavior

```bash
curl -fsSL https://aeges.dev/install.sh | AEGES_VERSION=0.1.0 sh
```

The initial checked-in script installs the .NET tool package. A later
self-contained release installer can install into `~/.aeges/bin` or
`~/.local/bin` after native archives are published.

## macOS And Linux: Homebrew

Homebrew should be the friendly package-manager path once GitHub releases are
stable:

```bash
brew tap aeges-dev/tap
brew install aeges
```

The Homebrew formula should consume checksummed release archives, not build from
an arbitrary branch.

## Later Platform Packages

After the runtime daemon and service install flows stabilize:

- Windows: `winget`, Scoop, and possibly MSI
- Linux: deb/rpm packages for service-friendly installs
- macOS: Homebrew plus launchd service helper

Native packages should install the CLI, then delegate runtime setup to governed
Aeges commands such as `aeges telegram setup` and future agent service commands.

## Setup After Installation

Installation only puts the `aeges` command on the machine. Runtime setup remains
explicit:

```bash
aeges db migrate
aeges telegram setup
aeges telegram start
aeges agent start
```

## Current Priority

1. Keep the .NET tool package working in CI.
2. Keep `scripts/install.sh` working for local package sources.
3. Publish a signed/checksummed GitHub release artifact.
4. Add a Homebrew tap.
5. Add platform service packages only after daemon behavior is stable.
