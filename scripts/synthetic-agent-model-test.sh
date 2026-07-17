#!/usr/bin/env bash
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SQL_SH="$REPO_ROOT/scripts/sql.sh"

BASE_URL="${MODEL_BASE_URL:-http://192.168.0.216:11434/v1}"
MODEL="${MODEL_NAME:-gemma4-e4b-32k}"
SOURCE_ID=""
MAX_SOURCE_CHARS="${MAX_SOURCE_CHARS:-3500}"

usage() {
  cat <<'EOF'
Usage: ./scripts/synthetic-agent-model-test.sh [options]

Runs a synthetic prompt test directly against an OpenAI-compatible model endpoint,
using real source data from Postgres (without going through the app stack).

Options:
  --base-url <url>      Model API base URL (default: http://192.168.0.216:11434/v1)
  --model <name>        Model name (default: gemma4-e4b-32k)
  --source-id <guid>    Force a specific source row from DB
  --max-chars <n>       Max source chars injected into prompt (default: 3500)
  -h, --help            Show help

Examples:
  ./scripts/synthetic-agent-model-test.sh
  ./scripts/synthetic-agent-model-test.sh --base-url http://192.168.0.216:114334/v1
  ./scripts/synthetic-agent-model-test.sh --source-id 019f6fb0-7548-7812-b7aa-97d79703e09b
EOF
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --base-url)
      BASE_URL="$2"
      shift 2
      ;;
    --model)
      MODEL="$2"
      shift 2
      ;;
    --source-id)
      SOURCE_ID="$2"
      shift 2
      ;;
    --max-chars)
      MAX_SOURCE_CHARS="$2"
      shift 2
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo "Unknown option: $1" >&2
      usage
      exit 1
      ;;
  esac
done

if ! command -v curl >/dev/null 2>&1; then
  echo "Missing required command: curl" >&2
  exit 1
fi

if ! command -v jq >/dev/null 2>&1; then
  echo "Missing required command: jq" >&2
  exit 1
fi

if [[ ! -x "$SQL_SH" ]]; then
  echo "Missing executable SQL helper: $SQL_SH" >&2
  exit 1
fi

cd "$REPO_ROOT"

if [[ -n "$SOURCE_ID" ]]; then
  QUERY="select \"Id\", coalesce(\"Title\",''), \"Reference\", coalesce(\"YoutubeVideoId\",''), left(coalesce(\"Content\",''), ${MAX_SOURCE_CHARS}) from \"Sources\" where \"Id\"='${SOURCE_ID}' limit 1;"
else
  QUERY="select \"Id\", coalesce(\"Title\",''), \"Reference\", coalesce(\"YoutubeVideoId\",''), left(coalesce(\"Content\",''), ${MAX_SOURCE_CHARS}) from \"Sources\" where \"Kind\"='YouTube' and coalesce(\"Content\",'') <> '' and coalesce(\"YoutubeVideoId\",'') <> '' order by \"AddedAt\" desc limit 1;"
fi

RAW_ROW="$($SQL_SH -A -t -F $'\t' -c "$QUERY")"

if [[ -z "${RAW_ROW//[[:space:]]/}" ]]; then
  echo "No suitable source row found in DB." >&2
  exit 1
fi

SOURCE_DB_ID="$(printf '%s' "$RAW_ROW" | cut -f1)"
SOURCE_TITLE="$(printf '%s' "$RAW_ROW" | cut -f2 | tr '\r\n' ' ' | sed 's/[[:space:]]\+/ /g')"
SOURCE_REF="$(printf '%s' "$RAW_ROW" | cut -f3 | tr '\r\n' ' ' | sed 's/[[:space:]]\+/ /g')"
SOURCE_VIDEO_ID="$(printf '%s' "$RAW_ROW" | cut -f4 | tr '\r\n' ' ' | sed 's/[[:space:]]\+/ /g')"
SOURCE_CONTENT="$(printf '%s' "$RAW_ROW" | cut -f5- | sed 's/\r$//')"

if [[ -z "$SOURCE_TITLE" ]]; then
  SOURCE_TITLE="YouTube Source"
fi

if [[ -n "$SOURCE_VIDEO_ID" ]]; then
  YT_URL="https://www.youtube.com/watch?v=${SOURCE_VIDEO_ID}"
else
  YT_URL="$SOURCE_REF"
fi

SYSTEM_PROMPT="$(cat "$REPO_ROOT/SYSTEM_PROMPT.md")"

USER_PROMPT=$(cat <<EOF
Synthetic offline test mode:
- No tools are available in this test.
- Do not mention tools, JSON, analysis, explanations, or step labels.
- Output ONLY the final thread markdown body.

Task:
Draft a 4-segment Bluesky thread from this real source.

Hard constraints:
- Exactly 4 segments separated by --- on its own line.
- Each segment under 280 chars.
- Include exactly one YouTube embed using image markdown syntax.
- Place the YouTube embed in segment 1.
- No bold/italic/headings/post labels.

Source metadata:
- source_id: $SOURCE_DB_ID
- title: $SOURCE_TITLE
- youtube_url: $YT_URL

Source transcript/content excerpt:
$SOURCE_CONTENT
EOF
)

PAYLOAD="$(jq -n \
  --arg model "$MODEL" \
  --arg sp "$SYSTEM_PROMPT" \
  --arg up "$USER_PROMPT" \
  '{model:$model,temperature:0,messages:[{role:"system",content:$sp},{role:"user",content:$up}]}' )"

RESPONSE_FILE="$(mktemp)"
HTTP_STATUS="$(curl -sS -o "$RESPONSE_FILE" -w "%{http_code}" \
  -H "Content-Type: application/json" \
  -d "$PAYLOAD" \
  "$BASE_URL/chat/completions")"

if [[ "$HTTP_STATUS" != "200" ]]; then
  echo "Model request failed: HTTP $HTTP_STATUS" >&2
  cat "$RESPONSE_FILE" >&2 || true
  rm -f "$RESPONSE_FILE"
  exit 1
fi

CONTENT="$(jq -r '.choices[0].message.content // empty' "$RESPONSE_FILE")"
rm -f "$RESPONSE_FILE"

if [[ -z "$CONTENT" ]]; then
  echo "Model response did not contain message content." >&2
  exit 1
fi

SEGMENTS_COUNT="$(printf '%s\n' "$CONTENT" | awk 'BEGIN{n=1} /^---[[:space:]]*$/ {n++} END{print n}')"
HAS_EMBED=0
if printf '%s' "$CONTENT" | grep -Eq '!\[[^]]+\]\(https://www\.youtube\.com/watch\?v=[^)]+'; then
  HAS_EMBED=1
fi

HAS_PLAIN_YT_LINK=0
if printf '%s' "$CONTENT" | grep -Eq '\[[^]]+\]\(https://www\.youtube\.com/watch\?v=[^)]+'; then
  if ! printf '%s' "$CONTENT" | grep -Eq '!\[[^]]+\]\(https://www\.youtube\.com/watch\?v=[^)]+'; then
    HAS_PLAIN_YT_LINK=1
  fi
fi

MAX_SEG_LEN="$(printf '%s\n' "$CONTENT" | awk '
BEGIN{seg=""; max=0}
function emit(){
  gsub(/^[\n\r]+|[\n\r]+$/, "", seg)
  len=length(seg)
  if (len>max) max=len
  seg=""
}
/^---[[:space:]]*$/ { emit(); next }
{ seg = seg $0 "\n" }
END { emit(); print max }
')"

PASS=1
if [[ "$SEGMENTS_COUNT" -ne 4 ]]; then
  PASS=0
fi
if [[ "$HAS_EMBED" -ne 1 ]]; then
  PASS=0
fi
if [[ "$HAS_PLAIN_YT_LINK" -ne 0 ]]; then
  PASS=0
fi
if [[ "$MAX_SEG_LEN" -gt 280 ]]; then
  PASS=0
fi

echo "Synthetic test target"
echo "- base_url: $BASE_URL"
echo "- model: $MODEL"
echo "- source_id: $SOURCE_DB_ID"
echo "- source_title: $SOURCE_TITLE"
echo
echo "Checks"
echo "- segment_count == 4: $([[ "$SEGMENTS_COUNT" -eq 4 ]] && echo PASS || echo FAIL) (got $SEGMENTS_COUNT)"
echo "- has_youtube_embed: $([[ "$HAS_EMBED" -eq 1 ]] && echo PASS || echo FAIL)"
echo "- no_plain_youtube_link_only: $([[ "$HAS_PLAIN_YT_LINK" -eq 0 ]] && echo PASS || echo FAIL)"
echo "- max_segment_len <= 280: $([[ "$MAX_SEG_LEN" -le 280 ]] && echo PASS || echo FAIL) (got $MAX_SEG_LEN)"
echo
echo "Model output"
echo "-----------"
printf '%s\n' "$CONTENT"

if [[ "$PASS" -ne 1 ]]; then
  exit 2
fi
