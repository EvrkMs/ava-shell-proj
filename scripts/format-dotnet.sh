#!/usr/bin/env sh
set -eu

ROOT_DIR="$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)"

if ! dotnet format --version >/dev/null 2>&1; then
  echo "dotnet format not found." >&2
  echo "Install with: dotnet tool install -g dotnet-format" >&2
  exit 1
fi

TARGETS="
$ROOT_DIR/Api.Gateway/Api.Gateway.slnx
$ROOT_DIR/Auth.Service/Auth.Service.slnx
$ROOT_DIR/Safe.Service/Safe.Service.slnx
"

for target in $TARGETS; do
  if [ -f "$target" ]; then
    echo "Formatting $target"
    dotnet restore "$target"
    dotnet format "$target" --no-restore
  else
    echo "Skip missing: $target" >&2
  fi
done
