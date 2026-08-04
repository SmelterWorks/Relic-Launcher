#!/bin/sh
set -eu

install_dir="${RELIC_LAUNCHER_INSTALL_DIR:-/usr/lib/relic-launcher}"
exec "${install_dir}/RelicLauncher.App" "$@"
