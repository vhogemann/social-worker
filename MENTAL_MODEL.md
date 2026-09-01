# MENTAL_MODEL.md — social-worker

## Purpose

This document is the fast orientation map for agents and contributors. It captures how the system is structured, where key behavior lives, and how to validate changes.

Read this first, then use AGENTS.md for stricter policy and workflow rules.

## Product in one paragraph

social-worker is a local-first, Docker-only assistant for drafting and publishing social media threads. The user iterates in a chat + editor workflow, adds sources/media, formats per platform, explicitly confirms stage transitions, and publishes. Bluesky (threads) and LinkedIn (single-post MVP) are production-ready; other publishers are stubs.

## System shape

- Frontend: React + Vite + TypeScript + Tailwind in web/
- Backend: ASP.NET Core Minimal API + EF Core + Postgres in api/SocialWorker.Api/
- Infra services: Postgres, SearXNG, transcriber (Whisper), plus Docker tooling/e2e services
- Runtime model: Docker Compose is the canonical environment

## Where to look first

### Backend entry and wiring

- App startup: api/SocialWorker.Api/Program.cs
- Endpoint mapping + DB seed/migration startup flow: api/SocialWorker.Api/Infrastructure/Extensions/AppStartupExtensions.cs
- Feature registration extensions: api/SocialWorker.Api/Infrastructure/Extensions/*Extensions.cs

### Backend domains

- Chat: api/SocialWorker.Api/Features/Chat/
- Draft lifecycle: api/SocialWorker.Api/Features/Drafts/
- Publishing: api/SocialWorker.Api/Features/Publishing/
- Sources and media: api/SocialWorker.Api/Features/Sources/, api/SocialWorker.Api/Features/Media/
- Templates and Canvas: api/SocialWorker.Api/Features/Templates/, image-engine/
- Feed automation: api/SocialWorker.Api/Features/Feeds/
- Automation workflows & validation: api/SocialWorker.Api/Features/Workflows/
- Background tasks & durable queue: api/SocialWorker.Api/Features/BackgroundTasks/
- Background task worker pool: api/SocialWorker.Api/Features/BackgroundTasks/Services/BackgroundTaskHostedService.cs
- Auth/providers/accounts/users: api/SocialWorker.Api/Features/Auth/, Providers/, Accounts/, Users/
- API Key Subsystem: api/SocialWorker.Api/Infrastructure/Auth/ (ApiKeyService, ApiKeyAuthenticationHandler, SHA-256 hash storage)
- Model Context Protocol (MCP): api/SocialWorker.Api/Features/Mcp/ (JSON-RPC 2.0, SSE Transport `/api/mcp/sse`, Stateless `/api/mcp`, McpToolRegistry, McpResourceRegistry)
- Markdown AST & Thread Document: api/SocialWorker.Api/Infrastructure/Markdown/ (Markdig CommonMark AST parser for segments, media, links, and facets)


### Frontend entry and state

- App shell & routing: web/src/App.tsx, web/src/components/Navigation/AppHeader.tsx
- Workflows & Detail Dashboard: web/src/components/WorkflowsPanel/, web/src/components/WorkflowDetail/
- Drafts Library Page: web/src/components/Drafts/DraftsPage.tsx
- Full-width 2-pane Composer: web/src/components/Composer/ComposerPage.tsx
- Scoped Composer Draft Context: web/src/components/Composer/ComposerDraftContext.tsx (local draft, editor doc, autosave, activity cards, scoped sources)
- Scoped Chat Provider: web/src/api/chat.tsx (scoped assistant-ui runtime seeded from and autosaved to draft.chatHistory)
- Navigation & Breadcrumbs: web/src/components/Navigation/
- Template Studio & WYSIWYG Editor: web/src/components/TemplateStudio/
- Draft library cache: web/src/store/draftStore.ts
- API contract clients: web/src/api/

### Tests

- API unit/integration tests (442 tests): api/SocialWorker.Api.Tests/
- MCP & Stress test suites: scripts/mcp-stress-test-suite.mjs, scripts/test-mcp-e2e.mjs
- Web tests (Vitest): web/src/**/*.test.ts, web/src/**/*.test.tsx
- E2E (Playwright in Docker): e2e/ and scripts/run-e2e.sh

## Core user flow mental model

1. User opens/creates a draft (via Web UI or MCP server).
2. Chat and editor collaborate on thread content using CommonMark AST segmentation (`---`).
3. User attaches sources (URLs, YouTube, files); backend scrapes content, runs Whisper transcription, and extracts text.
4. Automation workflows poll RSS/YouTube feeds and autonomously generate drafts citing research/articles.
5. Platform thread variants are created/updated with server-enforced validation rules (character limits, facets, single-card embed constraints).
6. Stage transitions are explicit and server-enforced.
7. Publish is allowed only when validation rules are satisfied (readiness + character count + AST syntax checks).
8. Successful publish writes post records and updates thread stage to Sent.

## Model Context Protocol (MCP) & API Keys

- Endpoint: `/api/mcp` (Stateless JSON-RPC 2.0) and `/api/mcp/sse?api_key=...` (Server-Sent Events continuous stream).
- Tools (13): `list_drafts`, `get_draft`, `create_draft`, `update_draft`, `validate_draft`, `publish_thread`, `list_sources`, `add_source`, `fetch_source`, `list_workflows`, `trigger_workflow`, `list_feeds`, `trigger_feed_poll`.
- Resources: `socialworker://feeds`, `socialworker://workflows`, `socialworker://drafts/{id}`.
- Auth: `ApiKeyAuthenticationHandler` validates SHA-256 hashed keys (`sw_live_...`) routed through ASP.NET Core `PolicyScheme` ("Composite"). Dev key `sw_live_dev_admin_key_000000000000` seeded in development.

## Runtime and commands that matter most

- Fast hot-reloading dev stack (Vite HMR + dotnet watch): ./scripts/run-dev.sh
- Bring up stack: docker compose up --build
- Bring up stack with GPU: ./scripts/redeploy.sh --gpu
- Safe refresh of running stack: ./scripts/redeploy.sh
- API build (tooling profile): docker compose --profile tooling run --rm dotnet build
- API tests (tooling profile): docker compose --profile tooling run --rm dotnet test SocialWorker.Api.Tests/SocialWorker.Api.Tests.csproj
- MCP stress test: docker compose --profile tooling run --rm -v "$(pwd):/repo" -w /repo -e API_URL="http://api:8080" web-test node scripts/mcp-stress-test-suite.mjs
- Web test (tooling profile): docker compose --profile tooling run --rm web-test
- Web check & build (tooling profile): docker compose --profile tooling run --rm web-test "npm run build && npm run typecheck" (or docker compose build web)
- E2E: ./scripts/run-e2e.sh
- SQL against Postgres: ./scripts/sql.sh -c "select now();"


## Durable task queue behavior

- Unified PostgreSQL 16 `FOR UPDATE SKIP LOCKED` task queue table (`"BackgroundTasks"`) replaces in-memory channels and handles transcriptions, scrapes, chat summaries, and feed ingestion items.
- Queue processing is executed by `BackgroundTaskHostedService` with concurrent workers, crash recovery leases, and exponential backoff retry.
- Completed and failed task history is automatically pruned by `BackgroundTaskRetentionService` and inspectable via `/api/background-tasks`.

## Active roadmap center of gravity

- Canonical roadmap index: PLAN.md
- Current emphasis: Bluesky improvements, feed automation, quality/reliability hardening, and maintainability work

## Keep this file up-to-date

Update this file in the same PR whenever one of these changes happens:

- New top-level feature area or major directory move
- Backend startup/wiring changes (Program, extensions, endpoint grouping)
- Frontend app shell/routing/state ownership changes
- Validation/publishing gate behavior changes
- Build/test/redeploy command changes
- Roadmap source-of-truth location changes

Minimum update checklist:

1. Confirm "Where to look first" paths are still correct.
2. Confirm command examples still work in Docker-first workflow.
3. Confirm core flow steps still match real behavior.
4. Add or remove bullets rather than writing long prose.
5. Keep this file short enough to scan in under 2 minutes.

## Relationship to AGENTS.md

- AGENTS.md: policy, constraints, non-negotiable workflow requirements
- MENTAL_MODEL.md: rapid technical orientation and navigation map
