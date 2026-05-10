#!/bin/sh
set -eu

TOOL_PACKAGE="${AEGES_TOOL_PACKAGE:-Aeges.Cli}"
TOOL_COMMAND="${AEGES_TOOL_COMMAND:-aeges}"
VERSION="${AEGES_VERSION:-}"
PACKAGE_SOURCE="${AEGES_PACKAGE_SOURCE:-}"

usage() {
    cat <<'EOF'
Aeges installer

Installs or updates the Aeges CLI as a global .NET tool.

Usage:
  curl -fsSL https://raw.githubusercontent.com/aeges-dev/aeges/main/scripts/install.sh | sh
  wget -qO- https://raw.githubusercontent.com/aeges-dev/aeges/main/scripts/install.sh | sh

Options via environment variables:
  AEGES_VERSION=0.1.0-alpha.1
  AEGES_PACKAGE_SOURCE=/path/to/packages
  AEGES_TOOL_PACKAGE=Aeges.Cli
  AEGES_TOOL_COMMAND=aeges

Local checkout example:
  dotnet pack src/Aeges.Cli/Aeges.Cli.csproj -c Release
  AEGES_PACKAGE_SOURCE="$PWD/.artifacts/packages" AEGES_VERSION=0.1.0-alpha.1 sh scripts/install.sh
EOF
}

if [ "${1:-}" = "--help" ] || [ "${1:-}" = "-h" ]; then
    usage
    exit 0
fi

if ! command -v dotnet >/dev/null 2>&1; then
    echo "dotnet was not found on PATH." >&2
    echo "Install the .NET 10 SDK, then run this installer again." >&2
    echo "https://dotnet.microsoft.com/download" >&2
    exit 1
fi

TOOLS_DIR="${HOME}/.dotnet/tools"
mkdir -p "${HOME}/.aeges"

tool_update() {
    if [ -n "${VERSION}" ] && [ -n "${PACKAGE_SOURCE}" ]; then
        dotnet tool update --global "${TOOL_PACKAGE}" --version "${VERSION}" --add-source "${PACKAGE_SOURCE}"
    elif [ -n "${VERSION}" ]; then
        dotnet tool update --global "${TOOL_PACKAGE}" --version "${VERSION}"
    elif [ -n "${PACKAGE_SOURCE}" ]; then
        dotnet tool update --global "${TOOL_PACKAGE}" --add-source "${PACKAGE_SOURCE}"
    else
        dotnet tool update --global "${TOOL_PACKAGE}"
    fi
}

tool_install() {
    if [ -n "${VERSION}" ] && [ -n "${PACKAGE_SOURCE}" ]; then
        dotnet tool install --global "${TOOL_PACKAGE}" --version "${VERSION}" --add-source "${PACKAGE_SOURCE}"
    elif [ -n "${VERSION}" ]; then
        dotnet tool install --global "${TOOL_PACKAGE}" --version "${VERSION}"
    elif [ -n "${PACKAGE_SOURCE}" ]; then
        dotnet tool install --global "${TOOL_PACKAGE}" --add-source "${PACKAGE_SOURCE}"
    else
        dotnet tool install --global "${TOOL_PACKAGE}"
    fi
}

echo "Installing Aeges CLI..."
echo "Package: ${TOOL_PACKAGE}"
if [ -n "${VERSION}" ]; then
    echo "Version: ${VERSION}"
fi
if [ -n "${PACKAGE_SOURCE}" ]; then
    echo "Source: ${PACKAGE_SOURCE}"
fi

# Use update first because it is idempotent when the tool is already installed.
# Fall back to install for first-time setup.
if tool_update; then
    :
else
    tool_install
fi

if command -v "${TOOL_COMMAND}" >/dev/null 2>&1; then
    echo "Aeges installed. Try: ${TOOL_COMMAND} db status"
else
    echo "Aeges installed, but '${TOOL_COMMAND}' is not on PATH yet."
    echo "Add the .NET tools directory to PATH:"
    echo "  export PATH=\"\$PATH:${TOOLS_DIR}\""
fi
