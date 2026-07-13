#!/usr/bin/env bash
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUTPUT_MD="${REPO_ROOT}/e2e/output/GETTING_STARTED.md"
OUTPUT_IMG_DIR="${REPO_ROOT}/e2e/output/getting-started"
DOC_MD="${REPO_ROOT}/GETTING_STARTED.md"
DOC_IMG_DIR="${REPO_ROOT}/docs/getting-started"

cd "$REPO_ROOT"

docker compose --profile e2e build e2e
docker compose --profile e2e run --rm e2e npx playwright test tests/generate-getting-started.spec.ts

if [[ ! -f "$OUTPUT_MD" ]]; then
  echo "Expected output markdown not found: $OUTPUT_MD" >&2
  exit 1
fi

if [[ ! -d "$OUTPUT_IMG_DIR" ]]; then
  echo "Expected screenshot directory not found: $OUTPUT_IMG_DIR" >&2
  exit 1
fi

mkdir -p "$DOC_IMG_DIR"
rm -f "$DOC_IMG_DIR"/*.png
cp "$OUTPUT_MD" "$DOC_MD"
cp "$OUTPUT_IMG_DIR"/*.png "$DOC_IMG_DIR"/

echo "GETTING_STARTED.md and docs/getting-started screenshots have been refreshed."
