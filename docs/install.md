# Install Roadmap

Aeges should be easy to install without hiding how the runtime works. The
installation path should mature in layers, starting with the native .NET tool
flow and expanding into platform package managers when releases are stable.

## Today: .NET Tool

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
dotnet tool uninstall --global Aeges.Cli
dotnet pack src/Aeges.Cli/Aeges.Cli.csproj -c Release
dotnet tool install --global Aeges.Cli \
  --add-source "$PWD/.artifacts/packages" \
  --version 0.1.0-alpha.1
```

## First Public Release: NuGet Tool

When Aeges is ready for public package publishing, the primary cross-platform
install command should become:

```bash
dotnet tool install --global Aeges.Cli
```

This keeps the first public distribution simple and works consistently on
Windows, macOS, and Linux for users who already have the .NET SDK.

## Next: Shell Installer

The shell installer should download a versioned release artifact, verify a
checksum, install without `sudo` by default, and add clear PATH guidance.

Target UX:

```bash
curl -fsSL https://aeges.dev/install.sh -o install.sh
sh install.sh
```

The script should support:

- macOS and Linux
- explicit version selection
- `~/.aeges/bin` or `~/.local/bin`
- checksum verification
- idempotent reinstall/update behavior

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

## Current Priority

1. Keep the .NET tool package working in CI.
2. Publish a signed/checksummed GitHub release artifact.
3. Add a shell installer around release artifacts.
4. Add a Homebrew tap.
5. Add platform service packages only after daemon behavior is stable.
