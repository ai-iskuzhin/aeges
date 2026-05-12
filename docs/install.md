# Installation

Aeges should be easy to install without hiding how the runtime works. The
installation path should mature in layers, starting with the native .NET tool
flow and expanding into platform package managers when releases are stable.

## Branch Channels

Aeges should use two public branch channels for installer scripts and docs:

```text
production    stable public channel
develop       preview integration channel
```

`production` is the branch that backs public install snippets and
`get.aeges.top`. `develop` is for testing unreleased installer changes before
they become public defaults. Versioned binaries still come from GitHub Releases,
not from branch contents.

Stable latest installer scripts are available through the static install site:

```text
https://get.aeges.top/install.sh
https://get.aeges.top/install.ps1
```

The matching raw branch files are:

```text
https://raw.githubusercontent.com/ai-iskuzhin/aeges/production/scripts/install.sh
https://raw.githubusercontent.com/ai-iskuzhin/aeges/production/scripts/install.ps1
```

Preview latest installer scripts use:

```text
https://raw.githubusercontent.com/ai-iskuzhin/aeges/develop/scripts/install.sh
https://raw.githubusercontent.com/ai-iskuzhin/aeges/develop/scripts/install.ps1
```

The repository default should be `production` for public stability. `develop`
remains the preview integration branch.

## Static Install Site

`get.aeges.top` is hosted from a separate static repository:

```text
~/work/aeges-static
```

That repository owns the GitHub Pages deployment, `public/CNAME`, the install
landing page, and public copies of:

```text
public/install.sh
public/install.ps1
```

The Aeges repository remains the source of truth for runtime code, release
packages, and release tags. The static repository is the stable web entrypoint
for installer scripts.

## Recommended Shell UX

The friendly macOS/Linux install path should feel like Docker-style installers:

```bash
curl -fsSL https://get.aeges.top/install.sh | sh
```

Equivalent forms should also work:

```bash
wget -qO- https://get.aeges.top/install.sh | sh
```

```bash
curl -fsSL https://get.aeges.top/install.sh -o install.sh
sh install.sh
```

For local development, use the checked-in installer from a local checkout:

```bash
dotnet pack src/Aeges.Cli/Aeges.Cli.csproj -c Release
AEGES_PACKAGE_SOURCE="$PWD/.artifacts/packages" \
AEGES_VERSION=0.1.0-alpha.4 \
sh scripts/install.sh
```

The script installs or updates the CLI as a global .NET tool. It is intentionally
thin: it does not install Codex, Telegram, background services, or machine-local
configuration. Those remain explicit runtime setup steps.

After the first install, users can update through the CLI itself:

```bash
aeges update
aeges update --version 0.1.0-alpha.4
```

`aeges update` uses the same release artifact contract as the shell installers:
it downloads `SHA256SUMS`, resolves the matching `Aeges.Cli` package, verifies
the package checksum, and runs `dotnet tool update --global` with the downloaded
package directory as a source. Local development builds can be installed with:

```bash
aeges update --version 0.1.0-alpha.4 --package-source "$PWD/.artifacts/packages"
```

For a GitHub release download, specify a version:

```bash
curl -fsSL https://get.aeges.top/install.sh | AEGES_VERSION=0.1.0-alpha.4 sh
```

Supported installer environment variables:

```text
AEGES_VERSION=0.1.0-alpha.4
AEGES_PACKAGE_SOURCE=/path/to/packages
AEGES_DOWNLOAD_BASE_URL=https://github.com/ai-iskuzhin/aeges/releases/latest/download
AEGES_GITHUB_REPOSITORY=ai-iskuzhin/aeges
AEGES_TOOL_PACKAGE=Aeges.Cli
AEGES_TOOL_COMMAND=aeges
```

The script requires the .NET SDK to already be available on `PATH`.
When `AEGES_VERSION` is set and no local package source is provided, it
downloads `Aeges.Cli.<version>.nupkg` and `SHA256SUMS` from the GitHub release,
verifies the checksum, then installs from the downloaded package directory.

## Windows PowerShell UX

Windows should use the same artifact contract through PowerShell:

```powershell
irm https://get.aeges.top/install.ps1 | iex
```

For a versioned GitHub release:

```powershell
$env:AEGES_VERSION = "0.1.0-alpha.4"
irm https://get.aeges.top/install.ps1 | iex
```

For a local checkout:

```powershell
dotnet pack src/Aeges.Cli/Aeges.Cli.csproj -c Release
.\scripts\install.ps1 -PackageSource .\.artifacts\packages -Version 0.1.0-alpha.4
```

`install.ps1` supports the same configuration surface as `install.sh`:

```text
AEGES_VERSION
AEGES_PACKAGE_SOURCE
AEGES_DOWNLOAD_BASE_URL
AEGES_GITHUB_REPOSITORY
AEGES_TOOL_PACKAGE
AEGES_TOOL_COMMAND
```

## .NET Tool

The CLI is packaged as a .NET tool. From a local checkout:

```bash
dotnet pack src/Aeges.Cli/Aeges.Cli.csproj -c Release
dotnet tool install --global Aeges.Cli \
  --add-source "$PWD/.artifacts/packages" \
  --version 0.1.0-alpha.4
```

If `aeges` is still not found, add the .NET tool directory to `PATH`:

```bash
export PATH="$PATH:$HOME/.dotnet/tools"
```

For repeated local installs during development:

```bash
dotnet pack src/Aeges.Cli/Aeges.Cli.csproj -c Release
AEGES_PACKAGE_SOURCE="$PWD/.artifacts/packages" \
AEGES_VERSION=0.1.0-alpha.4 \
sh scripts/install.sh
```

## First Public Release: NuGet Tool

Aeges can publish the CLI package to NuGet from version tags. Add a repository
secret named `NUGET_TOKEN` with a NuGet API key that can push `Aeges.Cli`.

Required NuGet setup:

1. Create or use a NuGet.org account.
2. Create an API key scoped to push packages.
3. Add the key to GitHub repository secrets as `NUGET_TOKEN`.
4. Push a version tag such as `v0.1.0-alpha.4`.

The release workflow packs `src/Aeges.Cli`, uploads the package to GitHub
Releases, and pushes the same `.nupkg` to NuGet when `NUGET_TOKEN` is present.
If the secret is missing, the GitHub release still succeeds and NuGet publishing
is skipped.

Once the package is published, the primary cross-platform install command is:

```bash
dotnet tool install --global Aeges.Cli
```

For prerelease packages:

```bash
dotnet tool install --global Aeges.Cli --prerelease
```

Updates can use the normal NuGet source once the package exists there:

```bash
dotnet tool update --global Aeges.Cli --prerelease
```

The Aeges-specific updater remains available and keeps the GitHub release
checksum path:

```bash
aeges update
```

## Release Shell Installer

The release shell installer should keep the same `curl | sh` UX while adding
release hardening:

- download a versioned release artifact from GitHub releases
- verify the package with `SHA256SUMS`
- install without `sudo` by default
- add clear PATH guidance
- support explicit version selection
- provide idempotent reinstall/update behavior

```bash
curl -fsSL https://get.aeges.top/install.sh | AEGES_VERSION=0.1.0 sh
```

The initial checked-in script installs the .NET tool package. A later
self-contained release installer can install into `~/.aeges/bin` or
`~/.local/bin` after native archives are published.

## Release Artifact Contract

GitHub releases should publish:

```text
install.sh
install.ps1
Aeges.Cli.<version>.nupkg
SHA256SUMS
```

For tag `v0.1.0-alpha.4`, the default remote package URL is:

```text
https://github.com/ai-iskuzhin/aeges/releases/download/v0.1.0-alpha.4/Aeges.Cli.0.1.0-alpha.4.nupkg
```

The release workflow builds, tests, packs the CLI tool, stages `install.sh` and
`install.ps1`, generates SHA-256 checksums, extracts the matching
`CHANGELOG.md` section, and creates a GitHub release with `gh release create`.
Tags named `v*` publish a release. Tags containing a hyphen, such as
`v0.1.0-alpha.4`, are marked as prereleases.

## macOS And Linux: Homebrew

Homebrew should be the friendly package-manager path once GitHub releases are
stable:

```bash
brew tap ai-iskuzhin/tap
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
2. Keep `scripts/install.sh` and `scripts/install.ps1` working for local package
   sources.
3. Publish a checksummed GitHub release artifact from version tags.
4. Add a Homebrew tap.
5. Add platform service packages only after daemon behavior is stable.
