#!/usr/bin/env bash
set -euo pipefail

mode="dev"
pull_images="false"

for arg in "$@"; do
  case "$arg" in
    --app)
      mode="app"
      ;;
    --pull)
      pull_images="true"
      ;;
    -h|--help)
      echo "Usage: ./scripts/redeploy.sh [--app] [--pull]"
      echo
      echo "  --app   Redeploy docker-compose.app.yml (published images)"
      echo "  --pull  Pull latest images before startup"
      exit 0
      ;;
    *)
      echo "Unknown option: $arg" >&2
      echo "Use --help for usage." >&2
      exit 1
      ;;
  esac
done

if [[ "$mode" == "app" ]]; then
  compose=(docker compose -f docker-compose.app.yml)
else
  compose=(docker compose)
fi

"${compose[@]}" down --remove-orphans

if [[ "$pull_images" == "true" ]]; then
  "${compose[@]}" pull
fi

if [[ "$mode" == "app" ]]; then
  "${compose[@]}" up -d
else
  "${compose[@]}" up -d --build
fi

"${compose[@]}" ps
"${compose[@]}" logs --tail 80 api
