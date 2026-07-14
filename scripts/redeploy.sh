#!/usr/bin/env bash
set -euo pipefail

mode="dev"
pull_images="false"
use_gpu="false"

for arg in "$@"; do
  case "$arg" in
    --app)
      mode="app"
      ;;
    --pull)
      pull_images="true"
      ;;
    --gpu)
      use_gpu="true"
      ;;
    -h|--help)
      echo "Usage: ./scripts/redeploy.sh [--app] [--pull] [--gpu]"
      echo
      echo "  --app   Redeploy docker-compose.app.yml (published images)"
      echo "  --pull  Pull latest images before startup"
      echo "  --gpu   Enable GPU override for transcriber in dev mode"
      exit 0
      ;;
    *)
      echo "Unknown option: $arg" >&2
      echo "Use --help for usage." >&2
      exit 1
      ;;
  esac
done

if [[ "$mode" == "app" && "$use_gpu" == "true" ]]; then
  echo "--gpu is only supported in dev mode (docker-compose.yml)." >&2
  exit 1
fi

if [[ "$mode" == "app" ]]; then
  compose=(docker compose -f docker-compose.app.yml)
elif [[ "$use_gpu" == "true" ]]; then
  compose=(docker compose -f docker-compose.yml -f docker-compose.gpu.yml)
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
