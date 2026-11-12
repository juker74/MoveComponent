#!/bin/bash
# Copy Pods dynamic frameworks into the app at build time.
# More reliable than "Embed Frameworks" for SOURCE_ROOT xcframeworks under Pods/.
set -euo pipefail

PROJECT_ROOT="${PROJECT_DIR:-$(pwd)}"
PODS_ROOT="${PROJECT_ROOT}/Pods"
DEST="${TARGET_BUILD_DIR}/${FRAMEWORKS_FOLDER_PATH}"

if [[ ! -d "${PODS_ROOT}" ]]; then
  echo "[AT] copy_dynamic_pods: Pods not found, skip"
  exit 0
fi

mkdir -p "${DEST}"

is_dynamic() {
  local binary="$1"
  [[ -f "${binary}" ]] || return 1
  file "${binary}" | grep -q "dynamically linked shared library"
}

pick_slice() {
  local xc="$1"
  local slice=""
  if [[ "${PLATFORM_NAME:-iphoneos}" == "iphonesimulator" ]]; then
    slice=$(find "${xc}" -maxdepth 1 -type d \( -name 'ios-*-simulator' -o -name 'ios-*-maccatalyst' \) | head -1)
    # prefer simulator over catalyst
    slice=$(find "${xc}" -maxdepth 1 -type d -name 'ios-*-simulator' | head -1)
  else
    slice=$(find "${xc}" -maxdepth 1 -type d -name 'ios-arm64' | head -1)
    if [[ -z "${slice}" ]]; then
      slice=$(find "${xc}" -maxdepth 1 -type d -name 'ios-arm64_*' ! -name '*simulator*' | head -1)
    fi
  fi
  echo "${slice}"
}

copy_framework_dir() {
  local fw_dir="$1"
  local name
  name="$(basename "${fw_dir}" .framework)"
  local binary="${fw_dir}/${name}"
  if ! is_dynamic "${binary}"; then
    return 0
  fi
  echo "[AT] copy_dynamic_pods: ${name}.framework"
  rm -rf "${DEST}/${name}.framework"
  ditto "${fw_dir}" "${DEST}/${name}.framework"

  if [[ -n "${EXPANDED_CODE_SIGN_IDENTITY:-}" && "${EXPANDED_CODE_SIGN_IDENTITY}" != "-" ]]; then
    codesign --force --sign "${EXPANDED_CODE_SIGN_IDENTITY}" \
      --preserve-metadata=identifier,entitlements,flags \
      "${DEST}/${name}.framework" || true
  fi
}

# 1) Dynamic xcframeworks under Pods (must have Info.plist — incomplete Dynamic packages break Xcode)
while IFS= read -r -d '' xc; do
  case "${xc}" in
    *"/Target Support Files/"*|*.dSYM*) continue ;;
  esac
  [[ -f "${xc}/Info.plist" ]] || continue
  slice="$(pick_slice "${xc}")"
  [[ -n "${slice}" ]] || continue
  name="$(basename "${xc}" .xcframework)"
  fw="${slice}/${name}.framework"
  [[ -d "${fw}" ]] || continue
  copy_framework_dir "${fw}"
done < <(find "${PODS_ROOT}" -type d -name '*.xcframework' -print0)

# 2) Loose dynamic .framework (not inside xcframework)
while IFS= read -r -d '' fw; do
  case "${fw}" in
    *.xcframework/*|*/Target\ Support\ Files/*|*.dSYM*) continue ;;
  esac
  copy_framework_dir "${fw}"
done < <(find "${PODS_ROOT}" -type d -name '*.framework' -print0)

echo "[AT] copy_dynamic_pods: done -> ${DEST}"
ls -la "${DEST}" || true
