#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
COMPOSE_BASE="${REPO_ROOT}/docker-compose.yml"
COMPOSE_DEV="${REPO_ROOT}/docker-compose.dev.yml"
ENV_FILE="${REPO_ROOT}/.env"

ACTION="up"
DETACH="-d"
DO_BUILD=""
SERVICES=()

usage() {
  cat <<'EOF'
Usage: ./scripts/run-dev.sh [options]

Starts the optimized local development stack with live hot-reloading:
- Web: Vite HMR on http://localhost:8100 (proxies /api to backend)
- API: dotnet watch on http://localhost:8101 (live rebuild on C# file changes)
- Image Engine: tsx watch on http://localhost:8103
- Database: in-memory tuned PostgreSQL 16 on localhost:5432

Options:
  --build       Rebuild base images before starting
  --down        Stop and tear down the dev stack
  --logs [svc]  View logs (all services or specific service)
  --fg          Run in foreground (interactive output)
  -h, --help    Show this help
EOF
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --build)
      DO_BUILD="--build"
      shift
      ;;
    --down)
      ACTION="down"
      shift
      ;;
    --logs)
      ACTION="logs"
      shift
      ;;
    --fg)
      DETACH=""
      shift
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      if [[ "$ACTION" == "logs" ]]; then
        break
      else
        SERVICES+=("$1")
        shift
      fi
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

if [[ "$ACTION" == "down" ]]; then
  docker compose -f "$COMPOSE_BASE" -f "$COMPOSE_DEV" down --remove-orphans
  echo "Dev stack stopped."
  exit 0
fi

if [[ "$ACTION" == "logs" ]]; then
  shift || true
  docker compose -f "$COMPOSE_BASE" -f "$COMPOSE_DEV" logs -f "$@"
  exit 0
fi

if [[ ! -f "$ENV_FILE" ]]; then
  if [[ -f "${REPO_ROOT}/.env.example" ]]; then
    echo "Creating .env from .env.example..."
    cp "${REPO_ROOT}/.env.example" "$ENV_FILE"
  fi
fi

cd "$REPO_ROOT"

echo "Starting optimized dev stack..."
if [[ ${#SERVICES[@]} -gt 0 ]]; then
  docker compose -f "$COMPOSE_BASE" -f "$COMPOSE_DEV" up ${DETACH} ${DO_BUILD} "${SERVICES[@]}"
else
  docker compose -f "$COMPOSE_BASE" -f "$COMPOSE_DEV" up ${DETACH} ${DO_BUILD}
fi

if [[ -n "$DETACH" ]]; then
  echo ""
  echo "social-worker dev stack is running with hot-reload:"
  echo "  Web (Vite HMR): http://localhost:8100"
  echo "  API (.NET watch): http://localhost:8101"
  echo "  Image Engine:   http://localhost:8103"
  echo "  Transcriber:    http://localhost:8102"
  echo "  PostgreSQL:     localhost:5432"
  echo ""
  echo "Logs: ./scripts/run-dev.sh --logs"
  echo "Stop: ./scripts/run-dev.sh --down"
fi
