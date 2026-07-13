#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
COMPOSE_FILE="${REPO_ROOT}/docker-compose.app.yml"
ENV_FILE="${REPO_ROOT}/.env"
DO_PULL=0

usage() {
  cat <<'EOF'
Usage: scripts/run-app.sh [--pull]

Options:
  --pull    Pull latest images before starting
  -h, --help  Show this help
EOF
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --pull)
      DO_PULL=1
      shift
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo "Unknown option: $1" >&2
      usage >&2
      exit 1
      ;;
  esac
done

if ! command -v docker >/dev/null 2>&1; then
  echo "docker is required but was not found in PATH." >&2
  exit 1
fi

if ! docker compose version >/dev/null 2>&1; then
  echo "docker compose is required but not available." >&2
  exit 1
fi

if [[ ! -f "$COMPOSE_FILE" ]]; then
  echo "Missing compose file: $COMPOSE_FILE" >&2
  exit 1
fi

if [[ ! -f "$ENV_FILE" ]]; then
  echo "Missing .env file at $ENV_FILE" >&2
  echo "Create it first: cp .env.example .env" >&2
  exit 1
fi

set -a
source "$ENV_FILE"
set +a

require_var() {
  local var_name="$1"
  local value="${!var_name:-}"
  if [[ -z "$value" ]]; then
    echo "Required env var is missing or empty: $var_name" >&2
    exit 1
  fi
}

require_var "LLM__ApiKey"
require_var "Auth__JwtSecret"
require_var "Auth__DbEncryptionKey"

if [[ "$LLM__ApiKey" == "or-..." ]]; then
  echo "LLM__ApiKey is still set to placeholder value." >&2
  exit 1
fi

if [[ ${#Auth__JwtSecret} -lt 32 ]]; then
  echo "Auth__JwtSecret must be at least 32 characters." >&2
  exit 1
fi

if [[ $DO_PULL -eq 1 ]]; then
  docker compose -f "$COMPOSE_FILE" pull
fi

docker compose -f "$COMPOSE_FILE" up -d

echo "social-worker is starting."
echo "Web: http://localhost:8100"
echo "API: http://localhost:8101"
