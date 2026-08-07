#!/usr/bin/env bash
set -euo pipefail

usage() {
  echo "Usage: github-release-notes.sh --dist DIR [--tag TAG --repo OWNER/REPO]" >&2
  exit 1
}

DIST_DIR=""
TAG=""
REPO=""

while [ "$#" -gt 0 ]; do
  case "$1" in
    --dist)
      DIST_DIR="${2:?}"
      shift 2
      ;;
    --tag)
      TAG="${2:?}"
      shift 2
      ;;
    --repo)
      REPO="${2:?}"
      shift 2
      ;;
    -h|--help)
      usage
      ;;
    *)
      echo "Unknown argument: $1" >&2
      usage
      ;;
  esac
done

if [ -z "$DIST_DIR" ] || [ ! -d "$DIST_DIR" ]; then
  echo "Missing or invalid --dist directory: ${DIST_DIR:-}" >&2
  exit 1
fi

if [ -n "$TAG" ] && [ -z "$REPO" ]; then
  echo "--repo is required when --tag is set" >&2
  exit 1
fi

if [ -n "$TAG" ]; then
  printf 'Read the [changelog](https://github.com/%s/blob/%s/CHANGELOG.md) for changes in this release.\n\n' "$REPO" "$TAG"
fi

echo "## SHA256 checksums"
echo ""
echo "| File | SHA256 |"
echo "| --- | --- |"

mapfile -t FILES < <(find "$DIST_DIR" -type f | sort)
if [ "${#FILES[@]}" -eq 0 ]; then
  echo "No files found under ${DIST_DIR}" >&2
  exit 1
fi

for file in "${FILES[@]}"; do
  name="$(basename "$file")"
  hash="$(sha256sum "$file" | awk '{print $1}')"
  printf '| `%s` | `%s` |\n' "$name" "$hash"
done
