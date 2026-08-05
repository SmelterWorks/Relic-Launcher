#!/usr/bin/env bash
set -euo pipefail

if [ "$#" -ne 2 ]; then
  echo "Usage: create-zip.sh OUTPUT_ZIP SOURCE_DIR" >&2
  exit 1
fi

OUTPUT="${1:?}"
SOURCE_DIR="${2:?}"

if [ ! -d "${SOURCE_DIR}" ]; then
  echo "Source directory not found: ${SOURCE_DIR}" >&2
  exit 1
fi

OUTPUT_DIR="$(cd "$(dirname "${OUTPUT}")" && pwd)"
OUTPUT_NAME="$(basename "${OUTPUT}")"
OUTPUT_PATH="${OUTPUT_DIR}/${OUTPUT_NAME}"

if command -v zip >/dev/null 2>&1; then
  (cd "${SOURCE_DIR}" && zip -qr "${OUTPUT_PATH}" .)
else
  tar -acf "${OUTPUT_PATH}" -C "${SOURCE_DIR}" .
fi

echo "Created ${OUTPUT_PATH}"
