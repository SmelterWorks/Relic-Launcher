#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'EOF'
Usage: build-packages.sh --version VERSION --publish-dir DIR --portable-dir DIR --output-dir DIR

Build Windows portable zip and NSIS installer from publish folders.
EOF
}

VERSION=""
PUBLISH_DIR=""
PORTABLE_DIR=""
OUTPUT_DIR=""

while [ "$#" -gt 0 ]; do
  case "$1" in
    --version)
      VERSION="${2:?}"
      shift 2
      ;;
    --publish-dir)
      PUBLISH_DIR="${2:?}"
      shift 2
      ;;
    --portable-dir)
      PORTABLE_DIR="${2:?}"
      shift 2
      ;;
    --output-dir)
      OUTPUT_DIR="${2:?}"
      shift 2
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo "Unknown argument: $1" >&2
      usage >&2
      exit 1
      ;;
  esac
done

if [ -z "$VERSION" ] || [ -z "$PUBLISH_DIR" ] || [ -z "$PORTABLE_DIR" ] || [ -z "$OUTPUT_DIR" ]; then
  usage >&2
  exit 1
fi

if [ ! -f "${PUBLISH_DIR}/RelicLauncher.App.exe" ]; then
  echo "Publish directory is missing RelicLauncher.App.exe: ${PUBLISH_DIR}" >&2
  exit 1
fi

if [ ! -f "${PORTABLE_DIR}/RelicLauncher.App.exe" ]; then
  echo "Portable directory is missing RelicLauncher.App.exe: ${PORTABLE_DIR}" >&2
  exit 1
fi

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
NSIS_SCRIPT="${ROOT_DIR}/packaging/windows/relic-launcher.nsi"
WORK_DIR="$(mktemp -d)"
trap 'rm -rf "${WORK_DIR}"' EXIT

mkdir -p "${OUTPUT_DIR}"
OUTPUT_DIR="$(cd "${OUTPUT_DIR}" && pwd)"
PUBLISH_DIR="$(cd "${PUBLISH_DIR}" && pwd)"
PORTABLE_DIR="$(cd "${PORTABLE_DIR}" && pwd)"

PORTABLE_ZIP="${OUTPUT_DIR}/relic-launcher-${VERSION}-win-x64-portable.zip"
SETUP_EXE="${OUTPUT_DIR}/relic-launcher-${VERSION}-win-x64-setup.exe"
ICON_FILE="${ROOT_DIR}/assets/icons/icon.ico"

if [ ! -f "${ICON_FILE}" ]; then
  echo "Icon file not found: ${ICON_FILE}" >&2
  exit 1
fi

portable_stage="${WORK_DIR}/portable"
mkdir -p "${portable_stage}"
cp "${PORTABLE_DIR}/RelicLauncher.App.exe" "${portable_stage}/RelicLauncher.App.exe"
find "${portable_stage}" -name '*.pdb' -delete

if command -v zip >/dev/null 2>&1; then
  (cd "${portable_stage}" && zip -qr "${PORTABLE_ZIP}" .)
else
  tar -acf "${PORTABLE_ZIP}" -C "${portable_stage}" .
fi

echo "Created ${PORTABLE_ZIP}"

install_stage="${WORK_DIR}/installer"
mkdir -p "${install_stage}"
cp -a "${PUBLISH_DIR}/." "${install_stage}/"

if ! command -v makensis >/dev/null 2>&1; then
  echo "makensis not found. Skipping NSIS installer." >&2
  exit 0
fi

to_windows_path() {
  if command -v cygpath >/dev/null 2>&1; then
    cygpath -w "$1"
  elif command -v winepath >/dev/null 2>&1; then
    winepath -w "$1"
  else
    printf '%s' "$1"
  fi
}

PUBLISH_DIR_WIN="$(to_windows_path "${install_stage}")"
ICON_FILE_WIN="$(to_windows_path "${ICON_FILE}")"
OUT_FILE_WIN="$(to_windows_path "${SETUP_EXE}")"

makensis \
  "/DVERSION=${VERSION}" \
  "/DPUBLISH_DIR=${PUBLISH_DIR_WIN}" \
  "/DICON_FILE=${ICON_FILE_WIN}" \
  "/DOUT_FILE=${OUT_FILE_WIN}" \
  "${NSIS_SCRIPT}"

echo "Created ${SETUP_EXE}"
