#!/usr/bin/env bash
set -euo pipefail

script_root="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
rid="${1:-}"

if [[ -z "${rid}" ]]; then
  case "$(uname -s)-$(uname -m)" in
    Linux-x86_64) rid="linux-x64" ;;
    Darwin-arm64) rid="osx-arm64" ;;
    Darwin-x86_64) rid="osx-x64" ;;
    *) echo "Unsupported native host: $(uname -s)-$(uname -m)" >&2; exit 2 ;;
  esac
fi

case "${rid}:$(uname -s):$(uname -m)" in
  linux-x64:Linux:x86_64|osx-arm64:Darwin:arm64|osx-x64:Darwin:x86_64) ;;
  *) echo "RID ${rid} does not match host $(uname -s)/$(uname -m)" >&2; exit 2 ;;
esac

for tool in python3 cmake cc; do
  if ! command -v "${tool}" >/dev/null 2>&1; then
    echo "Required build tool '${tool}' is not available." >&2
    exit 2
  fi
done

exec python3 "${script_root}/build.py" --rid "${rid}"
