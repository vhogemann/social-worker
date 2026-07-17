#!/usr/bin/env bash
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
DB_SERVICE="${SW_DB_SERVICE:-db}"
DB_NAME="${SW_DB_NAME:-socialworker}"
DB_USER="${SW_DB_USER:-postgres}"

usage() {
  cat <<'EOF'
Usage: ./scripts/sql.sh [options] [psql options...]

Executes SQL against the Postgres container in docker compose.

Options:
  -c, --command <sql>   Execute one SQL command
  -f, --file <path>     Execute SQL from file
      --service <name>  Docker compose db service name (default: db)
      --db <name>       Database name (default: socialworker)
      --user <name>     Database user (default: postgres)
  -h, --help            Show this help

Examples:
  ./scripts/sql.sh -c "select now();"
  ./scripts/sql.sh -A -F $'\t' -c "select \"Id\", \"Title\" from \"Drafts\" limit 5;"
  ./scripts/sql.sh -f /tmp/query.sql
  cat /tmp/query.sql | ./scripts/sql.sh -A -t
EOF
}

SQL_COMMAND=""
SQL_FILE=""
PSQL_ARGS=()

while [[ $# -gt 0 ]]; do
  case "$1" in
    -c|--command)
      if [[ $# -lt 2 ]]; then
        echo "Missing value for $1" >&2
        usage
        exit 1
      fi
      SQL_COMMAND="$2"
      shift 2
      ;;
    -f|--file)
      if [[ $# -lt 2 ]]; then
        echo "Missing value for $1" >&2
        usage
        exit 1
      fi
      SQL_FILE="$2"
      shift 2
      ;;
    --service)
      if [[ $# -lt 2 ]]; then
        echo "Missing value for $1" >&2
        usage
        exit 1
      fi
      DB_SERVICE="$2"
      shift 2
      ;;
    --db)
      if [[ $# -lt 2 ]]; then
        echo "Missing value for $1" >&2
        usage
        exit 1
      fi
      DB_NAME="$2"
      shift 2
      ;;
    --user)
      if [[ $# -lt 2 ]]; then
        echo "Missing value for $1" >&2
        usage
        exit 1
      fi
      DB_USER="$2"
      shift 2
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    --)
      shift
      PSQL_ARGS+=("$@")
      break
      ;;
    *)
      PSQL_ARGS+=("$1")
      shift
      ;;
  esac
done

if [[ -n "$SQL_COMMAND" && -n "$SQL_FILE" ]]; then
  echo "Use either --command or --file, not both." >&2
  exit 1
fi

if [[ -n "$SQL_FILE" && ! -f "$SQL_FILE" ]]; then
  echo "SQL file not found: $SQL_FILE" >&2
  exit 1
fi

if [[ -z "$SQL_COMMAND" && -z "$SQL_FILE" && -t 0 ]]; then
  echo "Provide --command, --file, or pipe SQL on stdin." >&2
  usage
  exit 1
fi

cd "$REPO_ROOT"

CMD=(docker compose exec -T "$DB_SERVICE" psql -U "$DB_USER" -d "$DB_NAME" -P pager=off)
CMD+=("${PSQL_ARGS[@]}")

if [[ -n "$SQL_COMMAND" ]]; then
  CMD+=(-c "$SQL_COMMAND")
elif [[ -n "$SQL_FILE" ]]; then
  CMD+=(-f "$SQL_FILE")
fi

"${CMD[@]}"