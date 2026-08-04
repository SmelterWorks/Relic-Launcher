#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'EOF'
Usage: build-app-bundle.sh --version VERSION --publish-dir DIR --output-dir DIR --rid RID

Build a Relic Launcher.app bundle and zip it for macOS publish output.
EOF
}

VERSION=""
PUBLISH_DIR=""
OUTPUT_DIR=""
RID=""

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
    --output-dir)
      OUTPUT_DIR="${2:?}"
      shift 2
      ;;
    --rid)
      RID="${2:?}"
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

if [ -z "$VERSION" ] || [ -z "$PUBLISH_DIR" ] || [ -z "$OUTPUT_DIR" ] || [ -z "$RID" ]; then
  usage >&2
  exit 1
fi

if [ ! -f "${PUBLISH_DIR}/RelicLauncher.App" ]; then
  echo "Publish directory is missing RelicLauncher.App: ${PUBLISH_DIR}" >&2
  exit 1
fi

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
ICON_SRC="${ROOT_DIR}/assets/icons/icon-256.png"
WORK_DIR="$(mktemp -d)"
trap 'rm -rf "${WORK_DIR}"' EXIT

APP_NAME="Relic Launcher.app"
APP_ROOT="${WORK_DIR}/${APP_NAME}"
mkdir -p "${APP_ROOT}/Contents/MacOS" "${APP_ROOT}/Contents/Resources"
cp -a "${PUBLISH_DIR}/." "${APP_ROOT}/Contents/MacOS/"
chmod +x "${APP_ROOT}/Contents/MacOS/RelicLauncher.App"
install -m644 "${ICON_SRC}" "${APP_ROOT}/Contents/Resources/AppIcon.png"

cat > "${APP_ROOT}/Contents/Info.plist" <<EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleDevelopmentRegion</key>
  <string>en</string>
  <key>CFBundleDisplayName</key>
  <string>Relic Launcher</string>
  <key>CFBundleExecutable</key>
  <string>RelicLauncher.App</string>
  <key>CFBundleIconFile</key>
  <string>AppIcon.png</string>
  <key>CFBundleIdentifier</key>
  <string>works.smelter.reliclauncher</string>
  <key>CFBundleInfoDictionaryVersion</key>
  <string>6.0</string>
  <key>CFBundleName</key>
  <string>Relic Launcher</string>
  <key>CFBundlePackageType</key>
  <string>APPL</string>
  <key>CFBundleShortVersionString</key>
  <string>${VERSION}</string>
  <key>CFBundleVersion</key>
  <string>${VERSION}</string>
  <key>LSMinimumSystemVersion</key>
  <string>13.0</string>
  <key>NSHighResolutionCapable</key>
  <true/>
</dict>
</plist>
EOF

mkdir -p "${OUTPUT_DIR}"
OUT_ZIP="${OUTPUT_DIR}/relic-launcher-${VERSION}-${RID}.app.zip"
(
  cd "${WORK_DIR}"
  zip -qry "${OUT_ZIP}" "${APP_NAME}"
)

echo "Built ${OUT_ZIP}"
