#!/usr/bin/env bash
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
COMPOSE_FILE="${REPO_ROOT}/docker-compose.e2e.yml"
KEEP_UP=0
BUILD=0

usage() {
  cat <<'EOF'
Usage: ./scripts/run-e2e.sh [options] [playwright args...]

Options:
  --build      Build e2e/api/web images before running tests
  --keep-up    Keep e2e stack running after tests finish
  --help       Show this help message

Examples:
  ./scripts/run-e2e.sh
  ./scripts/run-e2e.sh tests/chat.spec.ts
  ./scripts/run-e2e.sh -- --grep "slash validate"
  ./scripts/run-e2e.sh --build --keep-up tests/generate-getting-started.spec.ts
EOF
}

PLAYWRIGHT_ARGS=()

while [[ $# -gt 0 ]]; do
  case "$1" in
    --build)
      BUILD=1
      ;;
    --keep-up)
      KEEP_UP=1
      ;;
    --help|-h)
      usage
      exit 0
      ;;
    --)
      shift
      PLAYWRIGHT_ARGS+=("$@")
      break
      ;;
    *)
      PLAYWRIGHT_ARGS+=("$1")
      ;;
  esac
  shift
done

cd "${REPO_ROOT}"

if [[ ${BUILD} -eq 1 ]]; then
  docker compose -f "${COMPOSE_FILE}" build transcriber-e2e api-e2e web-e2e e2e
fi

docker compose -f "${COMPOSE_FILE}" up -d --no-build db-e2e searxng-e2e transcriber-e2e api-e2e web-e2e

cleanup() {
  if [[ ${KEEP_UP} -eq 0 ]]; then
    docker compose -f "${COMPOSE_FILE}" down -v --remove-orphans
  fi
}

trap cleanup EXIT

DEMO_LLM_PROFILE=getting-started docker compose -f "${COMPOSE_FILE}" run --rm e2e npx playwright test "${PLAYWRIGHT_ARGS[@]}"
