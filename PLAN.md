# social-worker — plan & status

## Vision

Local-first, Docker-only multi-modal assistant for composing and publishing social media **threads**. A chat/composer UI lets the user draft a thread with LLM help, attach sources and media, adapt to each target platform, confirm, and publish.

**v1 target**: Bluesky publishing working end-to-end. Twitter, LinkedIn, Facebook, Instagram stubbed.

---

## Stack

| Layer | Technology |
|---|---|
| Backend | .NET 10 Minimal API + EF Core + Postgres 16 |
| Frontend | Vite + React + TypeScript + Tailwind |
| LLM | `OpenAI` .NET SDK with configurable `BaseUrl` (OpenAI / OpenRouter / Ollama) |
| Auth | JWT bearer + opaque refresh tokens, BCrypt passwords |
| Search | Brave Search API or local SearXNG (configurable) |
| Images | SkiaSharp (resize, code image rendering) |
| Local HTTPS | Caddy + mkcert |

---

## Running locally

```bash
# one-time cert bootstrap (requires mkcert on host)
./scripts/bootstrap.sh

# CPU
docker compose up --build

# Linux + NVidia GPU (for Ollama)
docker compose -f docker-compose.yml -f docker-compose.gpu.yml up --build
```

Domains after bootstrap:
- `https://social-worker.localtest` — app
- `https://api.social-worker.localtest` — API

---

## What is built and working

### Core infrastructure
- [x] Docker Compose stack: `db`, `api`, `web`, `searxng`, `adminer`, `proxy`, `ollama` (profile `local-llm`)
- [x] EF Core migrations applied automatically on startup
- [x] Local HTTPS via Caddy + mkcert (`scripts/bootstrap.sh`)
- [x] JWT auth with refresh tokens; single admin user seeded at startup

### Data model (all migrated)
- [x] `AppUser`, `Draft`, `ThreadSegment`, `Source`, `PlatformThread`, `Post`, `MediaAsset`, `BrandVoicePrompt`, `LlmProvider`

### Backend features
- [x] Draft CRUD (`GET/POST/PATCH/DELETE /api/drafts`)
- [x] Thread segments — reconciled from `---`-split markdown on every draft save
- [x] Sources — URL fetch+cache (readability extraction), file upload (PDF/md/txt text extraction), notes, image references
- [x] Media uploads — SkiaSharp resize, SHA-256 deduplication, per-draft storage volume
- [x] Chat SSE streaming — `text/event-stream`, multi-round tool execution
- [x] Chat history — persisted to `Draft.ChatHistory` (JSON), summarized/compacted via `Draft.ChatSummary` + `LastSummarizedMessageCount`
- [x] LLM providers — DB-backed config, per-user preference, connectivity test endpoint
- [x] Bluesky publishing — app-password flow, images per segment, platform thread lifecycle
- [x] Platform publisher stubs — Twitter, LinkedIn, Facebook, Instagram (`NotImplemented`)
- [x] Brand voice prompts — CRUD library, default prompt injection into system prompt
- [x] Platform limits validation — Bluesky 300-grapheme limit, image/YouTube conflict checks

### LLM tools (12 active)
| Tool | What it does |
|---|---|
| `replace_editor_content` | Replaces full draft markdown |
| `propose_stage_transition` | Proposes Draft→Ready→Sent transition |
| `validate_draft` | Checks character limits, image counts, conflicts |
| `add_source` | Adds URL / YouTube / file reference as source |
| `list_sources` | Lists sources attached to draft |
| `fetch_source` | Returns cached text content of a source |
| `view_image` | Returns base64 image for vision-capable models |
| `web_search` | Brave or SearXNG web search |
| `image_search` | Image search returning direct URLs |
| `add_image_source` | Downloads image from URL, stores as MediaAsset |
| `publish` | Publishes platform thread (Bluesky working) |
| `render_code_blocks` | Renders ``` fences as syntax-highlighted PNG images |

### Frontend features
- [x] Chat panel — SSE streaming, tool call rendering, stage-transition accept buttons
- [x] Markdown editor — CodeMirror 6, vim mode, debounced autosave, drag-and-drop / paste upload
- [x] Thread preview — per-segment cards, media preview, alt-text editor, copy button, YouTube embeds, link cards
- [x] Code block rendering — "Render as image" button per code fence in preview cards
- [x] Draft list sidebar — create, switch, archive, delete (with confirmation)
- [x] Stage stepper — platform tabs + Draft/Ready/Sent visual stepper
- [x] Settings modal — Account (password change), Users (admin CRUD), AI Providers (CRUD + test + preference)
- [x] Login page + auth guard

### Code image rendering (`Features/CodeImages/`)
- [x] `CodeBlockParser` — regex extraction of ``` fences from markdown
- [x] `CodeTokenizer` — subclasses `CodeColorizerBase` (ColorCode.Core), produces colored runs via scope tree walk
- [x] `CodeImageRenderer` — SkiaSharp canvas: rounded card, Mac-style dots, language label, line numbers, JetBrains Mono (embedded font)
- [x] Two themes: Dark (Dracula) and Light (One Light)
- [x] `POST /api/drafts/{draftId}/code-image` endpoint
- [x] `render_code_blocks` chat tool — replaces fences with `![code snippet](media://uuid)` in draft content
- [x] 38/38 tests passing (including 6 renderer + 6 parser tests)

---

## What is not yet built

### Near-term
- [ ] Web search tool integration (SearXNG container is running; `web_search` tool wired but needs end-to-end test). Tracked: `docs/SEARCH_TOOL.md`
- [ ] Platform variant generation (`generate_platform_variants` tool — adapts canonical thread to per-platform char limits, preview as columns)
- [ ] Thread reply/expansion (append to a published Bluesky thread, view replies)

### Platform publishers (stubs)
- [ ] Twitter/X publisher
- [ ] LinkedIn publisher
- [ ] Facebook publisher
- [ ] Instagram publisher

### Future / out of scope for v1
- [ ] Multi-user / teams
- [ ] Scheduling / calendar
- [ ] Analytics / inbox
- [ ] Image generation (attach files only)
- [ ] OpenAPI codegen for frontend types (hand-synced for now)

---

## Implementation plans (docs/)

| File | Status |
|---|---|
| `docs/MVP.md` | Done — initial stack proof |
| `docs/AUTHENTICATION.md` | Done |
| `docs/LLM_PROVIDERS.md` | Done |
| `docs/THREAD_STAGES.md` | Done |
| `docs/CHAT_SERVICE_REFACTORING.md` | Done |
| `docs/CHAT_HISTORY_PERSISTENCE.md` | Done |
| `docs/IMAGE_UPLOADS.md` | Done |
| `docs/BRAND_VOICE_PROMPTS.md` | Done |
| `docs/SEARCH_TOOL.md` | Planned |
| `docs/REFACTORING_PLAN.md` | Ongoing |
