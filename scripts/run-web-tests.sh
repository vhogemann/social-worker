#!/usr/bin/env bash
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

usage() {
  cat <<'EOF'
Usage: ./scripts/run-web-tests.sh [vitest args...]

Runs frontend tests in Docker via the tooling profile web-test service.

Examples:
  ./scripts/run-web-tests.sh
  ./scripts/run-web-tests.sh src/components/ChatPanel/Thread/ThreadMessageTextPart.test.tsx
  ./scripts/run-web-tests.sh -- --reporter verbose
EOF
}

if [[ "${1:-}" == "-h" || "${1:-}" == "--help" ]]; then
  usage
  exit 0
fi

if ! command -v docker >/dev/null 2>&1; then
  echo "docker is required but was not found in PATH." >&2
  exit 1
fi

if ! docker compose version >/dev/null 2>&1; then
  echo "docker compose is required but not available." >&2
  exit 1
fi

cd "${REPO_ROOT}"

if [[ $# -eq 0 ]]; then
  docker compose --profile tooling run --rm web-test
else
  docker compose --profile tooling run --rm web-test "npm install && npm run test -- $*"
fi
