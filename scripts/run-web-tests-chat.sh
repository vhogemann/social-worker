#!/usr/bin/env bash
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

usage() {
  cat <<'EOF'
Usage: ./scripts/run-web-tests-chat.sh [extra vitest args...]

Runs the focused chat thread test subset in Docker.

Default test files:
  src/components/ChatPanel/Thread/ThreadMessageTextPart.test.tsx
  src/components/ChatPanel/Thread/ThreadMessageImageSearchPanel.test.ts
  src/components/ChatPanel/Thread/ThreadMessageSearchResultsPanel.test.ts

Examples:
  ./scripts/run-web-tests-chat.sh
  ./scripts/run-web-tests-chat.sh -- --reporter verbose
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

BASE_CMD="npm install && npm run test -- ThreadMessageTextPart.test.tsx ThreadMessageImageSearchPanel.test.ts ThreadMessageSearchResultsPanel.test.ts"

if [[ $# -gt 0 ]]; then
  docker compose --profile tooling run --rm web-test "${BASE_CMD} $*"
else
  docker compose --profile tooling run --rm web-test "${BASE_CMD}"
fi
