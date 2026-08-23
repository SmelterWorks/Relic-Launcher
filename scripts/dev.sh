#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$root"

dotnet watch run --project src/RelicLauncher.App/RelicLauncher.App.csproj "$@"
