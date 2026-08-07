#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$root"

echo "==> dotnet restore"
dotnet restore RelicLauncher.sln

echo "==> dotnet format"
dotnet format RelicLauncher.sln --verify-no-changes --severity error

echo "==> dotnet build"
dotnet build RelicLauncher.sln -c Release --no-restore

echo "==> dotnet test"
dotnet test RelicLauncher.sln -c Release --no-build

echo "==> self-check"
export RELIC_SELF_CHECK_ROOT="${TMPDIR:-/tmp}/relic-self-check-$$"
dotnet run --project src/RelicLauncher.App/RelicLauncher.App.csproj -c Release --no-build -- --self-check

echo "All checks passed."
