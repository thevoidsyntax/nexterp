#!/bin/bash
# SessionStart hook: installs backend (.NET) and frontend (npm) dependencies
# so builds, tests and linters work out of the box in this session.
set -uo pipefail

if [ "${CLAUDE_CODE_REMOTE:-}" != "true" ]; then
  exit 0
fi

REPO_ROOT="${CLAUDE_PROJECT_DIR:-$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)}"
cd "$REPO_ROOT"

status=0

# ---------- Backend (.NET) ----------
# All projects (main + *.UnitTests/*.ContractTests) target net10.0.
if ! command -v dotnet >/dev/null 2>&1; then
  echo "==> Installing .NET SDK..."
  DOTNET_INSTALL_DIR="$HOME/.dotnet"
  if curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh; then
    bash /tmp/dotnet-install.sh --channel 10.0 --install-dir "$DOTNET_INSTALL_DIR"
    echo "export DOTNET_ROOT=\"$DOTNET_INSTALL_DIR\"" >> "$CLAUDE_ENV_FILE"
    echo "export PATH=\"$DOTNET_INSTALL_DIR:\$PATH\"" >> "$CLAUDE_ENV_FILE"
    export DOTNET_ROOT="$DOTNET_INSTALL_DIR"
    export PATH="$DOTNET_INSTALL_DIR:$PATH"
  else
    echo "‼️  Could not download the .NET installer (network policy?) — skipping backend setup."
    status=1
  fi
fi

if command -v dotnet >/dev/null 2>&1; then
  echo "==> Restoring .NET projects..."
  for proj in ERP.API ERP.Application ERP.Domain ERP.Infrastructure ERP.Domain.UnitTests ERP.Application.UnitTests ERP.API.ContractTests; do
    csproj="$REPO_ROOT/$proj/$proj.csproj"
    if [ -f "$csproj" ]; then
      dotnet restore "$csproj" || { echo "‼️  restore failed for $proj"; status=1; }
    fi
  done
fi

# ---------- Frontend (npm) ----------
if [ -f "$REPO_ROOT/nextjs-frontend/package.json" ]; then
  echo "==> Installing frontend dependencies..."
  (cd "$REPO_ROOT/nextjs-frontend" && npm install) || { echo "‼️  npm install failed"; status=1; }
fi

exit "$status"
