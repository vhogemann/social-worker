# social-worker — Local Docker-first social post composer

## Vision

A local-first, Docker-only assistant for composing and publishing social media posts. A chat/composer UI helps the user draft **threads** (not just single posts) with LLM assistance, attach sources and media, adapt to each platform, confirm, and publish. v1 ships Bluesky publishing working end-to-end; Twitter, LinkedIn, Facebook, Instagram are stubbed.

## Core workflow (default path)

```
rough draft → add sources → iterate → improve → format per-platform → confirm → post
```

This is **thread-first**. A draft is an ordered thread of segments, not a single blob. The workflow moves through explicit stages; the user approves each stage transition in the UI. The model proposes transitions but cannot self-advance or publish without user confirmation.

## Architecture

```
social-worker/
├─ docker-compose.yml              # base: db, migrator, api, web, adminer, proxy, ollama(profile=local-llm, CPU)
├─ docker-compose.gpu.yml          # override: NVidia GPU reservation for ollama
├─ .env.example
├─ scripts/bootstrap.sh            # one-time: mkcert certs
├─ proxy/{Caddyfile, certs/}       # certs gitignored
├─ api/                            # .NET 10 Minimal API + EF Core
└─ web/                            # Vite + React + TS + Tailwind
```

No pnpm workspaces, no shared TS package — frontend talks to API over HTTPS; types hand-synced for v1 (OpenAPI codegen later if it grows).

## Containers

- **db** — `postgres:16-alpine`, volume `pgdata`, healthcheck.
- **migrator** — one-shot, same image as `api`, entrypoint `dotnet ef database update --no-build`. Depends on db healthy, exits after.
- **api** — .NET 10 Minimal API. Internal port 8080. Reads `LLM__*`, `ConnectionStrings__Default`, `Bluesky__*`, `Secrets__*` from env.
- **web** — multi-stage Dockerfile (node build → nginx serve). Proxies `/api` to `api:8080` via nginx so frontend uses same-origin.
- **adminer** — `adminer:latest` on :8081.
- **proxy** — Caddy + mkcert. Terminates HTTPS for the three local domains.
- **ollama** — profile `local-llm`. CPU by default. GPU via `docker-compose.gpu.yml` override (NVidia runtime + `deploy.resources.reservations.devices`).

### Host modes

- **Mac host** — `docker compose --profile local-llm up` (CPU Ollama).
- **Linux host + NVidia** — `docker compose --profile local-llm -f docker-compose.yml -f docker-compose.gpu.yml up`. Requires `nvidia-container-toolkit` on host.

### One host-side step (unavoidable)

`scripts/bootstrap.sh` runs `mkcert -install` and generates certs for the three local domains into `proxy/certs/`. Only Docker + mkcert required on host.

## Backend (`api/`, .NET 10 Minimal API + EF Core)

```
api/SocialWorker.Api/
├─ Program.cs
├─ appsettings.json
├─ Data/
│  ├─ AppDbContext.cs
│  ├─ Entities/  { Account, Draft, ThreadSegment, Source, PlatformVariant, Post, MediaAsset, BrandVoicePrompt }
│  └─ Migrations/
├─ Features/
│  ├─ Chat/         { ChatEndpoint, ChatService, SSE stream, tools }
│  ├─ Drafts/       { CRUD, stage transitions }
│  ├─ Threads/      { segment CRUD, reorder }
│  ├─ Sources/      { add url|file|note, fetch+cache url, upload file/image, extract text }
│  ├─ Media/        { upload, store to /media volume }
│  ├─ Prompts/      { brand voice library CRUD }
│  ├─ Accounts/     { list/add/delete, encrypted tokens }
│  └─ Publishing/
│     ├─ IPublisher.cs
│     ├─ PlatformLimits.cs          # per-platform char/grapheme limits + splitters
│     ├─ BlueskyPublisher.cs        # working, v1
│     ├─ TwitterPublisher.cs        # stub: NotImplemented + auth URL
│     ├─ LinkedInPublisher.cs       # stub
│     ├─ FacebookPublisher.cs       # stub
│     └─ InstagramPublisher.cs     # stub
└─ Infrastructure/
   ├─ Llm/   { IChatClient via Microsoft.Extensions.AI.OpenAI, BaseUrl configurable }
   ├─ Secrets/ { AES with env key for token storage }
   ├─ PlatformAuth/ { Bluesky app-password, OAuth helpers (stubs) }
   └─ Sources/ { UrlFetcher (readability extraction), FileTextExtractor (pdf/md/txt) }
```

### LLM abstraction

Single `OpenAIClient` (from `OpenAI` .NET SDK) configured from `LLM__*` env vars. All three providers speak OpenAI-compatible `/v1/chat/completions`:

| Provider | BaseUrl | ApiKey | Model example |
|---|---|---|---|
| OpenAI | (default) | `sk-...` | `gpt-4o-mini` |
| OpenRouter | `https://openrouter.ai/api/v1` | `or-...` | `anthropic/claude-3.5-sonnet` |
| Ollama | `http://ollama:11434/v1` | `dummy` | `llama3.1` |

Chat endpoint streams via SSE (`text/event-stream`). The model uses function-calling tools (below) to manipulate the draft.

### Stage-aware tools exposed to the LLM

- `create_segment(content, media?)`, `update_segment(id, content)`, `reorder_segments(ids[])`, `delete_segment(id)`
- `add_source(kind: url|file|note, ref, content?)` → server fetches/caches URL text or stores note
- `list_sources()`, `fetch_source(id)` → returns cached readable text to the model
- `set_stage(stage)` — proposes transition; UI must accept (server rejects if user hasn't approved)
- `generate_platform_variants(platforms[])` — adapts the canonical thread into per-platform variants (handles X 280-char splits, Bluesky 300 grapheme, LinkedIn long-form)
- `publish(platform)` — only allowed when `Stage=Ready` and variants confirmed; otherwise returns an error to the model

### Working platform for v1: Bluesky

App-password flow (no OAuth review, no callback) — fastest to publish end-to-end. Other 4 platforms implement `IPublisher` returning `NotImplemented` + an auth URL placeholder; UI shows them as "coming soon".

## Frontend (`web/`, Vite + React + TS + Tailwind)

```
web/src/
├─ main.tsx
├─ App.tsx                          # sidebar (drafts) + main (chat/composer)
├─ api/                             # typed fetch wrappers
├─ components/
│  ├─ ChatPanel/                    # SSE chat, tool-call rendering, stage-transition accept buttons
│  ├─ StageStepper/                  # RoughDraft → Sourcing → Refining → Formatting → Ready → Sent
│  ├─ Composer/
│  │  ├─ ThreadView/                 # stacked segment cards with media + char counts
│  │  ├─ PlatformColumns/            # one column per target platform showing its variant
│  │  └─ SourceList/                 # sources attached to this draft
│  ├─ DraftList/                     # sidebar
│  ├─ MediaDropzone/                 # attach images/files
│  ├─ BrandPrompts/                  # CRUD UI
│  └─ PlatformStatus/                # connected/disabled per platform
├─ hooks/  { useSSEChat, useDrafts, useThreads, useSources, useAccounts }
└─ store/  { zustand: drafts + active conversation + stage state }
```

Deps: `@tanstack/react-query`, `zustand`, `tailwindcss`, `lucide-react`, `react-markdown`. SSE consumed with native `EventSource`.

### Preview UX

Thread shown as stacked cards (one per segment) with media preview and char/segment counters. When platform variants are generated, the composer switches to **per-platform columns** — one column per target platform — each showing that platform's adapted thread. User confirms each variant before the draft reaches `Ready`.

## DB schema (EF Core entities)

- `Account` (Id, Platform, Handle, CredentialsEncrypted, Status)
- `Draft` (Id, Title, Stage, Status, CreatedAt, UpdatedAt)
  - `Stage` enum: `RoughDraft, Sourcing, Refining, Formatting, Ready, Sent`
  - `Status`: `Editing, Archived, Deleted`
- `ThreadSegment` (Id, DraftId, Position, Content, MediaAssetId?) — ordered
- `Source` (Id, DraftId, Kind, Ref, CachedContent, ExtractedText, AddedAt)
  - `Kind`: `Url, File, Note`
- `PlatformVariant` (Id, DraftId, Platform, SegmentsJson, Status) — per-platform rendered thread
- `Post` (Id, DraftId, PlatformVariantId, SegmentIndex, Platform, RemoteId, PostedAt, Error) — one row per published segment per platform
- `MediaAsset` (Id, DraftId, FilePath, MimeType, AltText, Usage: Post|Reference)
- `BrandVoicePrompt` (Id, Name, Body, IsDefault)

Migrator applies migrations on `compose up`.

## Local HTTPS (Caddy + mkcert)

Caddyfile routes:
- `social-worker.localtest` → `web:80`
- `api.social-worker.localtest` → `api:8080`
- `db.social-worker.localtest` → `adminer:8080`

`scripts/bootstrap.sh` generates certs into `proxy/certs/` (gitignored). Host prerequisite: `mkcert` (and `nss`/`firefox` extras only if using Firefox).

## `.env.example`

```
LLM__Provider=OpenRouter            # OpenAI | OpenRouter | Ollama
LLM__BaseUrl=https://openrouter.ai/api/v1
LLM__ApiKey=or-...
LLM__Model=anthropic/claude-3.5-sonnet

Ollama__Host=http://ollama:11434     # used when Provider=Ollama

Bluesky__Identifier=
Bluesky__AppPassword=

ConnectionStrings__Default=Host=db;Database=socialworker;Username=postgres;Password=postgres
Secrets__TokenEncryptionKey=       # 32-byte base64

Postgres__Password=postgres
```

## v1 deliverable scope

1. Monorepo skeleton, all Dockerfiles, `docker-compose.yml` + `docker-compose.gpu.yml`, `.gitignore`.
2. `api/` Minimal API + EF migrations for the 7 entities + migrator service.
3. Chat endpoint streaming SSE with the stage-aware tool set.
4. Drafts + Threads (segments) + Sources (url/file/note + fetch & extract) + Media + Prompts + Accounts CRUD endpoints.
5. `generate_platform_variants` + per-platform limits/splitters (X 280, Bluesky 300 grapheme, LinkedIn long-form).
6. `web/` ChatPanel + StageStepper + Composer (ThreadView + PlatformColumns + SourceList) + DraftList + MediaDropzone + BrandPrompts + PlatformStatus.
7. `BlueskyPublisher` actually posting a thread (text + single image per segment).
8. Stubs for Twitter/LinkedIn/Facebook/Instagram.
9. `scripts/bootstrap.sh` + README.

## Run commands (target)

```
# one-time
./scripts/bootstrap.sh

# Mac / CPU
docker compose --profile local-llm up --build

# Linux + NVidia
docker compose --profile local-llm -f docker-compose.yml -f docker-compose.gpu.yml up --build

# pull a local model (once)
docker exec social-worker-ollama ollama pull llama3.1
```

## Decisions log

- **Stack**: Vite+React+TS frontend, .NET 10 Minimal API backend, Postgres via EF Core.
- **Thread-first**: a draft is an ordered thread of segments, not a single post.
- **Workflow**: rough draft → sources → refine → format → confirm → post, with explicit user-approved stage transitions.
- **Sources**: URLs (fetch + cache readable text), file uploads (extract text), reference images, free-text notes/quotes.
- **Per-platform**: generate all target variants on demand; preview as per-platform columns; user confirms each.
- **LLM**: `Microsoft.Extensions.AI` + `OpenAI` .NET SDK with configurable `BaseUrl` → OpenAI / OpenRouter / Ollama.
- **Ollama**: in compose as opt-in `local-llm` profile; CPU default, NVidia GPU via override file (Mac vs Linux/NVidia).
- **Working platform v1**: Bluesky (app password). Others stubbed.
- **Local HTTPS**: Caddy + mkcert; one host bootstrap step.
- **Auth**: none for v1 (single user).
- **Docker only**: no Node/dotnet on host; only Docker + mkcert.
- **Monorepo**: loose — `web/` and `api/` folders, no shared TS package, types hand-synced for v1.

## MVP progress

A scoped MVP was built first to de-risk the core stack before layering on v1 features. Scope and rationale live in `docs/MVP.md`; discovery notes in `docs/UI-DISCOVERY.md` and `docs/UI-LIBRARIES.md`.

### Done

- Project scaffold: `.gitignore`, `.env.example`, `docker-compose.yml` (CPU-only, no Postgres/Caddy/Adminer yet)
- API (`api/SocialWorker.Api/`): Minimal API with `POST /api/chat` SSE endpoint, OpenAI-compatible streaming via OpenRouter, single `replace_editor_content(markdown)` tool, in-memory `EditorState` singleton, Vercel data-stream v1 wire format (`x-vercel-ai-data-stream: v1` header), multi-round tool execution (max 3 rounds)
- Web (`web/`): Vite + React + TS + Tailwind, assistant-ui `useDataStreamRuntime` against same-origin `/api/chat`, CodeMirror 6 + `@replit/codemirror-vim` + `@codemirror/lang-markdown`, `react-resizable-panels` split layout, `@tanstack/react-hotkeys` for `Mod+J`/`Mod+K` focus switching, zustand editor store with `applyExternal` version-bump to drive CM6 dispatch
- Multi-stage Dockerfiles for both services (SDK/node build → aspnet/nginx runtime); nginx same-origin `/api/` proxy with `proxy_buffering off` for SSE
- Both images build and run under Docker; host ports 8100 (web) → 80, 8101 (api) → 8080
- Layout fix: app locked to viewport (`h-screen` + `overflow-hidden`), each panel scrolls independently (`min-h-0` + `overflow-hidden` wrappers)
- Editor read-access fix: frontend sends current editor content in the chat request body (`editor` field); backend writes it to `EditorState` and injects it into the system prompt before the model sees the conversation, so the model can read, spellcheck, and edit user-typed content

### Verified end-to-end

- `docker compose up -d --build` brings up both containers
- Chat streams from OpenRouter through the API into assistant-ui
- `replace_editor_content` tool call updates the editor in real time
- CodeMirror vim modes (normal/insert) and markdown highlighting work
- User-typed editor content is visible to the model (spellcheck, rewrite, etc.)
- `Mod+J` / `Mod+K` switch focus between chat and editor

### Current model

`moonshotai/kimi-k2.5` via OpenRouter (cheap, OpenAI-compatible). Configurable via `.env` `LLM__Model`.

### Next: layer v1 features

With the MVP proving the stack, the remaining v1 work is to add the pieces the MVP deliberately omitted, in this order.

**Authentication** — **Done.** User accounts, password hashing, JWT bearer auth with sliding-window refreshes, and draft ownership scoping are implemented. See [AUTHENTICATION.md](./docs/AUTHENTICATION.md) for the full details.

**Configurable LLM Providers** — **Done.** Admin-managed AI providers (OpenRouter, Ollama) with connectivity testing, model autocomplete suggestions, and per-user preference resolution at request time. See [LLM_PROVIDERS.md](./docs/LLM_PROVIDERS.md) for the full details.

**Thread Stages & Segment Reconciliation** — **In Progress.** Reconciling editor markdown splits into `ThreadSegments` database rows, and implementing the visual Stage Stepper interface with LLM transitions. See [THREAD_STAGES.md](./docs/THREAD_STAGES.md) for design and progress tracking.

**ChatService Refactoring** — **Done.** Extracted tools into separate services implementing `IChatTool` and structured the Chat namespace. See [CHAT_SERVICE_REFACTORING.md](./docs/CHAT_SERVICE_REFACTORING.md) for details. A broader API refactoring plan is tracked in [REFACTORING_PLAN.md](./docs/REFACTORING_PLAN.md).

**Chat History Persistence & Compaction** — **Done.** Implemented full history persistence in PostgreSQL and an asynchronous compaction model to keep the LLM context tight. See [CHAT_HISTORY_PERSISTENCE.md](./docs/CHAT_HISTORY_PERSISTENCE.md) for details.

**Web Search Tool Integration** — **Planned.** Implement a web search tool for the agent using either Brave Search API or a local SearXNG Docker container. See [SEARCH_TOOL.md](./docs/SEARCH_TOOL.md) for details.

**Thread Expansion & Interactions (Bluesky replies)** — **Planned.** Enable appending new posts/replies to an already published thread, and viewing/interacting with replies from Bluesky within the app UI/chat. See [SEARCH_TOOL.md](./docs/SEARCH_TOOL.md) for details.

With that done, the remaining v1 items are:

1. ~~Postgres + EF Core + migrator service + the 7 entities~~ — **done** (Draft + ThreadSegment; see "v1 step 1 & 2" below)
2. ~~Reconcile the markdown editor with `ThreadSegment` rows (markdown is the view, segments are the source of truth)~~ — **In Progress**
3. ~~Drafts list + stage stepper (`RoughDraft → Sourcing → Refining → Formatting → Ready → Sent`) with server-enforced transitions~~ — **In Progress**
4. Sources: URL fetch+cache, file text extraction, notes
5. Platform variants + per-platform limits/splitters + per-platform preview columns
6. Bluesky publisher (app-password flow) end-to-end
7. Stubs for Twitter/LinkedIn/Facebook/Instagram
8. Caddy + mkcert local HTTPS + `scripts/bootstrap.sh`
9. Brand voice prompts CRUD

### v1 step 1 & 2: Database + persistent draft sessions

**Done.** Replaced the in-memory `EditorState` singleton with Postgres-backed drafts, enabling per-draft persistence and session switching.

#### Backend

- **EF Core + Npgsql**: `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.2 + `Microsoft.EntityFrameworkCore.Design` 10.0.0-preview.3.25171.6 added to `SocialWorker.Api.csproj`
- **Entities**: `Data/Entities/Draft.cs` (`Id`, `Title`, `Stage` enum, `Status` enum, `Content` markdown blob, timestamps), `Data/Entities/ThreadSegment.cs` (`Id`, `DraftId`, `Position`, `Content` — table exists, not yet reconciled with editor), `Data/Enums.cs` (`DraftStage`: RoughDraft…Sent, `DraftStatus`: Editing/Archived/Deleted)
- **DbContext**: `Data/AppDbContext.cs` with Draft + ThreadSegment configs, enum-to-string conversions, unique index on `(DraftId, Position)`
- **Migration**: `Migrations/20260707214840_InitialCreate` — auto-applied on startup via `db.Database.MigrateAsync()` in `Program.cs` (no separate migrator service needed)
- **Draft CRUD**: `Features/Drafts/DraftsEndpoint.cs` — `GET /api/drafts` (list, excludes deleted), `POST /api/drafts` (create), `GET /api/drafts/{id}` (single), `PATCH /api/drafts/{id}` (update title/content)
- **Chat → DB**: `ChatService.cs` replaced `EditorState` with `IServiceScopeFactory` + `AppDbContext`. Accepts `DraftId` in request body; loads draft from DB, injects `Content` into system prompt, saves content on `replace_editor_content` tool calls scoped to the correct draft (not "most recently updated" — fixed a bug where tool saves went to the wrong draft)
- **Removed**: `Features/Editor/EditorState.cs` (in-memory singleton, obsolete)
- **Docker**: `docker-compose.yml` — added `db` service (postgres:16-alpine, volume `pgdata`, healthcheck), `api` depends on `db` healthy, `ConnectionStrings__Default` env var. `dotnet-ef` tool installed in api Dockerfile build stage for migration generation
- **Config**: `appsettings.json` + `.env.example` — `ConnectionStrings__Default`, `Postgres__Password`

#### Frontend

- **Draft API**: `web/src/api/drafts.ts` — typed fetch wrappers for the 4 draft endpoints, `DraftDto` type
- **Draft store**: `web/src/store/draftStore.ts` — zustand: `drafts[]`, `activeDraftId`, `loadDrafts`, `createDraft`, `switchDraft`, `updateDraftTitle`, `saveDraftContent`. `loadDrafts` no longer sets `activeDraftId` (App.tsx handles initial selection)
- **Chat store**: `web/src/store/chatStore.ts` — zustand: in-memory `messagesByDraft` map (draftId → `ExportedMessageRepository`), `saveMessages`/`loadMessages`/`clearMessages`. Chat history persists across draft switches but not page reloads (backend persistence deferred)
- **DraftList sidebar**: `web/src/components/DraftList/DraftList.tsx` — fixed 56px left sidebar, "+ new" button, active draft indicator (absolute-positioned accent bar + accent title color). On switch: saves current chat + editor content to backend before loading new draft, then restores chat from in-memory store
- **Chat runtime**: `web/src/api/chat.tsx` — `useChatRuntime` sends `draftId` in request body. `ChatRuntimeManager` holds module-level `runtimeRef`. `saveCurrentChat(id)`/`restoreChat(id)` exported for DraftList to call on switch
- **App layout**: `web/src/App.tsx` — sidebar | split-panel layout. On mount: loads drafts, selects most recent (or creates one if none), sets editor doc, restores chat
- **Spinner**: `Thread.tsx` — `useThread((state) => state.isRunning)` shows "assistant is thinking..." with rotating ring above composer while streaming
- **Removed**: `__draft__:` text-prefix hack from server stream and `TextPart` parser — obsolete once frontend always sends `draftId`; was causing draft-switch bugs

#### Verified end-to-end

- `docker compose up --build` brings up db + api + web, auto-applies migration
- `GET/POST/PATCH /api/drafts` CRUD works via curl
- Chat streams with draft context; `replace_editor_content` saves to the correct draft
- Switching drafts preserves chat history (in-memory) and editor content (persisted to DB)
- Active draft highlighted in sidebar with accent bar + colored title
- Spinner shows during agent responses

### Authentication & Scoped Sessions

**Done.** Implemented secure multi-user capabilities, scoping draft access and LLM interactions directly to authenticated users.

#### Backend
- **Auth Infrastructure**: JWT tokens (1-hour expiration) and opaque refresh tokens (7-day sliding expiry). Password hashing via BCrypt.
- **Scoping**: All endpoints in `DraftsEndpoint.cs` and `ChatService.cs` updated to validate session identity and filter/modify records belonging only to the current user.
- **Endpoints**: Added `/api/auth/login`, `/api/auth/refresh`, `/api/auth/logout`, `/api/auth/me`, `/api/users/*` (admin CRUD), and `/api/account/password` (self-management).

#### Frontend
- **Auth Guard**: Full screen `LoginPage.tsx` and `AuthGuard.tsx` ensuring only logged-in users access the workspace.
- **Settings Integration**: Tabbed modal panels for Account (self-service profile & password update) and Users (admin CRUD table, deactivations, resets).
- **Usability Adjustments**: Moved Settings button to drafts list footer and removed default placeholders on Login page.

### Configurable LLM/AI Providers

**Done.** Replaces static environment variables with database-backed config mapping, enabling dynamic provider selection and connection testing.

#### Backend
- **Data Schema**: Created `LlmProviders` table and added `PreferredProviderId` foreign key to `AppUsers`. Added `AddLlmProviders` migration.
- **Startup Seeding**: Seeding detects LLM provider settings from env variables on first load to initialize default.
- **Chat Routing**: ChatService resolves active LLM provider configuration (checks user preference, falls back to the system default provider) dynamically at request time.
- **Connectivity Testing**: Added `POST /api/providers/test` endpoint allowing admins to dry-run connections using specific credentials.

#### Frontend
- **Providers Config**: Added Admin Settings 'providers' tab managing provider CRUD table, default badges, active status toggling, and connection test buttons with inline response messages.
- **User Preference**: Added provider picker dropdown in User Profile Account tab.
- **Autocomplete**: Integrated HTML5 `<datalist>` dropdown with common models (Claude, Llama, Gemini, phi, etc.) that adjusts suggestions dynamically.

### Segment Reconciliation, Stage Steppers, Draft Lifecycle, & Preview Mode

**Done.** Implemented segment splitting, platform stage stepper workflows, lock statuses, real-time thread previews with copy utilities, and test suites. Detailed documentation is tracked in [THREAD_STAGES.md](file:///Users/victorhogemann/Projects/social-worker/docs/THREAD_STAGES.md).

#### Backend
- **ThreadSegment Reconciliation**: Parsed draft markdown split on `---` lines to automatically synchronize the `ThreadSegments` table inside `POST` / `PATCH` endpoints and chat stream callbacks.
- **Platform Sinks**: Introduced the `PlatformThread` database entity allowing each target platform variant (Bluesky, Twitter, etc.) to progress through stages independently. Dropped the old draft-level `Stage` column.
- **Auto-Naming**: Integrated a title summarization agent helper `TryGenerateDraftTitleAsync` inside `ChatService` to name newly created drafts based on their first prompt.

#### Frontend
- **Real-Time Thread Preview**: Added an "edit" / "preview" selector at the top-right of the editor workspace. In Preview mode, the raw markdown splits are parsed in real-time and rendered as a vertically connected feed of simulated social post cards.
- **Segment Copy Buttons**: Added a clipboard copy button inside each preview card featuring a 1.5s visual "Copied!" confirmation checkmark.
- **Interactive Stepper & Platform Tabs**: Created `StageStepper.tsx` showing platform tabs selection and a simplified 3-stage stepper (`Draft → Ready → Sent`) corresponding to the active platform variant.
- **Editor & Composer Lock states**: Disabled editing (`editable={false}`) and assistant inputs when the active draft status is locked (`Sourcing` or `Formatting`), displaying a custom blur overlay banner.
- **Debounced Autosave**: Added a 1.5s debounced autosave inside the markdown editor.

#### Testing
- **Test Suites**: Added integration tests in `DraftTests.cs` (checking segment splits, cascade deletes, and unique constraints) and configured Vitest for frontend unit tests in `draftStore.test.ts` (lazy thread variant creations), `StageStepper.test.tsx` (stepper nodes), and `ThreadPreview.test.tsx` (splits and copy buttons).

#### Known limitations / deferred

- **Chat history not persisted to backend** — only in-memory on the web app; reload clears it (next step)
- **Twitter/LinkedIn/Facebook/Instagram publishers** — stubbed for v1; Bluesky is the working publisher sink

### Image Uploads, YouTube Embeds, LLM Vision, Clean Deletions, & Hashing Deduplication

**Done.** Implemented local image uploads, drag-and-drop/paste integration, YouTube metadata parsing, platform limits validation, dynamic vision-capability-aware assistant tools, a browser draft deletion confirmation flow with clean switching, and SHA-256 fingerprinting deduplication for both media images and document sources. Detailed plans are tracked in [IMAGE_UPLOADS.md](docs/IMAGE_UPLOADS.md).

#### Backend
- **Image Resizing & Storage**: Processed and stored uploaded images inside `/app/uploads/{draftId}/` via SkiaSharp, downscaling to maximum 1200px.
- **Dynamic Vision-Aware Tools**: Injected `ModelCapabilityProbe` to probe LLM endpoints for tool and vision support. Exposed the `view_image` tool schema to the assistant only if the active LLM provider supports vision.
- **Multimodal SSE Routing**: Implemented `view_image` execution returning a base64 JPEG block and structured it correctly for upstream OpenAI/Ollama endpoints. Tool execution outputs are stringified, and image arrays are appended as separate `user` messages.
- **YouTube metadata & Conflict Checks**: Extracted YouTube titles/embeds and enforced Bluesky platform constraints (rejecting transitions to `Ready` or `Sent` if a segment contains both images and YouTube embeds).
- **SHA-256 deduplication & Disk Cleanup**:
  - Implemented SHA-256 file fingerprinting on both image and text/PDF document uploads (`POST /api/drafts/{draftId}/media` and `POST /api/drafts/{draftId}/files`). If a file with the same SHA-256 exists, it creates a database row pointing to the existing file path or content and skips duplicate work.
  - Added sharing checks to draft and asset deletes to only remove files from disk if no other draft points to that file path.
- **Database Migrations**: Created and applied `AddMediaAssetSha256` and `AddSourceSha256` migrations.

#### Frontend
- **Drag-and-Drop & Clipboard Paste**: Enabled drag-and-drop/paste files inside the editor to upload instantly and insert markdown references.
- **Preview Alt-Text Editor**: Added collapsible alt-text inputs inside preview cards and highlighted Bluesky segment formatting warnings in amber.
- **Draft List Deletions**: Added confirmation prompts before deletions. Deleting an active draft resets the chat runtime and switches to the next available draft (or spawns a clean fallback draft).
- **Settings & Providers Edit**: Enabled editing of existing provider forms and rendered indicator badges for model capabilities.
