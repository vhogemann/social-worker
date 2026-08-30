#!/usr/bin/env bash
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SCENARIO="${1:-full}"

cd "${REPO_ROOT}"

if [[ ! -f "${REPO_ROOT}/.env.functional" && ! -f "${REPO_ROOT}/.env" ]]; then
  echo "WARNING: Neither .env.functional nor .env found. Using default configurations." >&2
fi

# If node is available on host, run directly. Otherwise run via docker with host networking.
if command -v node >/dev/null 2>&1; then
  node "${REPO_ROOT}/scripts/functional-test.mjs" "${SCENARIO}"
else
  docker compose --profile tooling run --rm -v "${REPO_ROOT}:/repo" -w /repo web-test node scripts/functional-test.mjs "${SCENARIO}"
fi

