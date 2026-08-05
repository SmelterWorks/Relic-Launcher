#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'EOF'
Usage: build-packages.sh --version VERSION --publish-dir DIR --output-dir DIR

Build AppImage, deb, rpm, and Arch pkg.tar.zst packages from a linux-x64 publish folder.
EOF
}

VERSION=""
PUBLISH_DIR=""
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

if [ -z "$VERSION" ] || [ -z "$PUBLISH_DIR" ] || [ -z "$OUTPUT_DIR" ]; then
  usage >&2
  exit 1
fi

if [ ! -f "${PUBLISH_DIR}/RelicLauncher.App" ]; then
  echo "Publish directory is missing RelicLauncher.App: ${PUBLISH_DIR}" >&2
  exit 1
fi

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
PACKAGING_DIR="${ROOT_DIR}/packaging/linux"
ICON_SRC="${ROOT_DIR}/assets/icons/icon-256.png"
WORK_DIR="$(mktemp -d)"
trap 'rm -rf "${WORK_DIR}"' EXIT

mkdir -p "${OUTPUT_DIR}"

DEB_VERSION="${VERSION//-/\~}-1"
RPM_VERSION="${VERSION//-/.}"
RPM_RELEASE="1"
ARCH_PKGVER="${VERSION//-/.}-1"
INSTALL_LIB="usr/lib/relic-launcher"
INSTALL_BIN="usr/bin/relic-launcher"
INSTALL_DESKTOP="usr/share/applications/relic-launcher.desktop"
INSTALL_ICON="usr/share/icons/hicolor/256x256/apps/relic-launcher.png"

stage_root() {
  local root="$1"
  mkdir -p "${root}/${INSTALL_LIB}"
  cp -a "${PUBLISH_DIR}/." "${root}/${INSTALL_LIB}/"
  install -Dm755 "${PACKAGING_DIR}/relic-launcher.sh" "${root}/${INSTALL_BIN}"
  install -Dm644 "${PACKAGING_DIR}/relic-launcher.desktop" "${root}/${INSTALL_DESKTOP}"
  install -Dm644 "${ICON_SRC}" "${root}/${INSTALL_ICON}"
}

build_deb() {
  local root="${WORK_DIR}/deb"
  mkdir -p "${root}/DEBIAN"
  stage_root "${root}"

  cat > "${root}/DEBIAN/control" <<EOF
Package: relic-launcher
Version: ${DEB_VERSION}
Section: games
Priority: optional
Architecture: amd64
Maintainer: Relic Launcher Contributors <41898282+github-actions[bot]@users.noreply.github.com>
Homepage: https://github.com/SmelterWorks/Relic-Launchder
Description: Unofficial desktop launcher for Vintage Story
 Relic Launcher installs and manages Vintage Story versions, mods, and saves.
EOF

  local out="${OUTPUT_DIR}/relic-launcher-${VERSION}-linux-x64.deb"
  dpkg-deb --build --root-owner-group "${root}" "${out}"
}

build_rpm() {
  local root="${WORK_DIR}/rpm"
  local buildroot="${root}/BUILDROOT/relic-launcher-${RPM_VERSION}-${RPM_RELEASE}.x86_64"
  local spec="${root}/SPECS/relic-launcher.spec"
  mkdir -p "${root}/"{BUILD,RPMS,SOURCES,SPECS,SRPMS,BUILDROOT}
  stage_root "${buildroot}"

  cat > "${spec}" <<EOF
Name: relic-launcher
Version: ${RPM_VERSION}
Release: ${RPM_RELEASE}
Summary: Unofficial desktop launcher for Vintage Story
License: BSD-0-Clause
URL: https://github.com/SmelterWorks/Relic-Launchder
BuildArch: x86_64

%description
Relic Launcher installs and manages Vintage Story versions, mods, and saves.

%prep
%build
%install

%files
%defattr(-,root,root,-)
/${INSTALL_BIN}
/${INSTALL_LIB}
/${INSTALL_DESKTOP}
/${INSTALL_ICON}
EOF

  local out="${OUTPUT_DIR}/relic-launcher-${VERSION}-linux-x64.rpm"
  rpmbuild -bb "${spec}" --define "_topdir ${root}"

  local built_rpm
  built_rpm="$(find "${root}/RPMS" -name 'relic-launcher-*.rpm' | head -n 1)"
  if [ -z "${built_rpm}" ]; then
    echo "RPM build did not produce an output package." >&2
    exit 1
  fi
  mv "${built_rpm}" "${out}"
}

build_arch_pkg() {
  local root="${WORK_DIR}/arch"
  stage_root "${root}"
  local installed_size
  installed_size="$(du -sb "${root}" | awk '{print $1}')"
  local builddate
  builddate="$(date -u +%s)"

  cat > "${root}/.PKGINFO" <<EOF
pkgname = relic-launcher
pkgver = ${ARCH_PKGVER}
pkgdesc = Unofficial desktop launcher for Vintage Story
url = https://github.com/SmelterWorks/Relic-Launchder
builddate = ${builddate}
packager = Relic Launcher Contributors
size = ${installed_size}
arch = x86_64
license = custom:BSD-0-Clause
EOF

  local out="${OUTPUT_DIR}/relic-launcher-${VERSION}-linux-x64.pkg.tar.zst"
  (
    cd "${root}"
    bsdtar --zstd -cf "${out}" .PKGINFO usr
  )
}

build_appimage() {
  local appdir="${WORK_DIR}/RelicLauncher.AppDir"
  mkdir -p "${appdir}/usr/lib/relic-launcher"
  cp -a "${PUBLISH_DIR}/." "${appdir}/usr/lib/relic-launcher/"
  install -Dm644 "${PACKAGING_DIR}/relic-launcher.desktop" "${appdir}/relic-launcher.desktop"
  install -Dm644 "${ICON_SRC}" "${appdir}/relic-launcher.png"
  install -Dm644 "${ICON_SRC}" "${appdir}/.DirIcon"

  cat > "${appdir}/AppRun" <<'EOF'
#!/bin/sh
set -eu
APPDIR="$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)"
export RELIC_LAUNCHER_INSTALL_DIR="${APPDIR}/usr/lib/relic-launcher"
exec "${RELIC_LAUNCHER_INSTALL_DIR}/RelicLauncher.App" "$@"
EOF
  chmod +x "${appdir}/AppRun"

  local appimagetool="${WORK_DIR}/appimagetool.AppImage"
  local appimagetool_bin="${WORK_DIR}/appimagetool-root/AppRun"
  if [ ! -x "${appimagetool_bin}" ]; then
    curl -fsSL \
      "https://github.com/AppImage/AppImageKit/releases/download/continuous/appimagetool-x86_64.AppImage" \
      -o "${appimagetool}"
    chmod +x "${appimagetool}"
    (
      cd "${WORK_DIR}"
      "${appimagetool}" --appimage-extract >/dev/null
      mv squashfs-root appimagetool-root
    )
  fi

  local out="${OUTPUT_DIR}/relic-launcher-${VERSION}-x86_64.AppImage"
  ARCH=x86_64 VERSION="${VERSION}" "${appimagetool_bin}" "${appdir}" "${out}"
}

build_deb
build_rpm
build_arch_pkg
build_appimage

echo "Built packages in ${OUTPUT_DIR}:"
ls -1 "${OUTPUT_DIR}"
