# MENTAL_MODEL.md — social-worker

## Purpose

This document is the fast orientation map for agents and contributors. It captures how the system is structured, where key behavior lives, and how to validate changes.

Read this first, then use AGENTS.md for stricter policy and workflow rules.

## Product in one paragraph

social-worker is a local-first, Docker-only assistant for drafting and publishing social media threads. The user iterates in a chat + editor workflow, adds sources/media, formats per platform, explicitly confirms stage transitions, and publishes. Bluesky (threads), LinkedIn (single-post personal profile and company page publishing), Meta Threads (multi-post threads, carousel media, token lifecycle, and resilient resume), and Twitter / X (OAuth 2.0 PKCE, token rotation, chained reply tweets, 280 weighted-char validation, t.co budgeting, and resilient resume) are production-ready; other publishers are stubs.

## System shape & project breakdown

| Project | Location | Technology Stack | Mental Model |
| :--- | :--- | :--- | :--- |
| **Backend API** | `api/SocialWorker.Api/` | .NET 10 Minimal API, EF Core, Postgres 16 | [api/MENTAL_MODEL.md](file:///home/vhogemann/Projects/social-worker/api/MENTAL_MODEL.md) |
| **Frontend Web** | `web/` | React 19, TypeScript, Vite, Tailwind CSS, Zustand | [web/MENTAL_MODEL.md](file:///home/vhogemann/Projects/social-worker/web/MENTAL_MODEL.md) |
| **Transcriber** | `transcriber/` | Python 3.11, FastAPI, OpenAI Whisper, yt-dlp | [transcriber/MENTAL_MODEL.md](file:///home/vhogemann/Projects/social-worker/transcriber/MENTAL_MODEL.md) |
| **Image Engine** | `image-engine/` | Node.js 22, Fastify, Sharp, Canvas, Zod | [image-engine/MENTAL_MODEL.md](file:///home/vhogemann/Projects/social-worker/image-engine/MENTAL_MODEL.md) |
| **E2E Testing** | `e2e/` | Playwright, TypeScript, Docker stack (8200/8201) | [e2e/MENTAL_MODEL.md](file:///home/vhogemann/Projects/social-worker/e2e/MENTAL_MODEL.md) |
| **Infra Services** | root / `searxng/` | PostgreSQL 16, SearXNG, RSSHub, Traefik Gateway | [docker-compose.yml](file:///home/vhogemann/Projects/social-worker/docker-compose.yml) |

## Where to look first

### Backend entry and wiring

- App startup: api/SocialWorker.Api/Program.cs
- Endpoint mapping + DB seed/migration startup flow: api/SocialWorker.Api/Infrastructure/Extensions/AppStartupExtensions.cs
- Feature registration extensions: api/SocialWorker.Api/Infrastructure/Extensions/*Extensions.cs

### Backend domains

- Chat: api/SocialWorker.Api/Features/Chat/
- Draft lifecycle: api/SocialWorker.Api/Features/Drafts/ (DraftsService, DraftSegmentService, DraftAutoRepairService agent self-correction feedback loop on validation rejections)
- Publishing: api/SocialWorker.Api/Features/Publishing/ (Bluesky, LinkedIn, MetaThreads, Twitter, Resilience/PublishOutcomeRecorder, PublishValidationService)
- Sources and media: api/SocialWorker.Api/Features/Sources/, api/SocialWorker.Api/Features/Sources/Instagram/ (Instagram profile feed bridge via RSSHub sidecar container with fallback to Business Discovery Graph API, FeedMediaExtractor for video/image parsing, credential provider, OAuth, setup guide in docs/INSTAGRAM_SETUP.md), api/SocialWorker.Api/Features/Media/
- Templates and Canvas: api/SocialWorker.Api/Features/Templates/, image-engine/
- Feed automation & RSSHub discovery: api/SocialWorker.Api/Features/Feeds/ (RssHubRouteResolver, RssHubFeedDiscoveryService supporting multi-platform URLs for Instagram, X, Telegram, Reddit, TikTok, Bluesky with RSSHub route resolution and fallbacks, FeedOrchestrationService retry feedback loop, FeedThreadGenerationService, FeedPollingService)
- Content calendar & scheduling engine: api/SocialWorker.Api/Features/Scheduling/ (Schedules, Slot Rules, Cronos recurrence, SlotWindowCalculator pacing, QueueService with Level 2 Time-Decay and Cascading Preemption, SchedulePriorityCalculator, ScheduleSlotEvaluatorService worker, ScheduleEvaluationHostedService recurring evaluator, ScheduledPublishingTaskHandler auto-repair before publishing, active queue deduplication & partial unique index, Review Outbox)
- Automation workflows & validation: api/SocialWorker.Api/Features/Workflows/ (IWorkflowSourceHandler, FeedWorkflowSourceHandler, DigestWorkflowSourceHandler, extensible sources)
- Reply Agents: api/SocialWorker.Api/Features/ReplyAgents/ (Independent `ReplyAgent` entity in user library, many-to-one workflow assignment via `AutomationWorkflow.ReplyAgentId`, direct post assignment to organic/external/third-party posts via `ReplyAgentPost` and `IExternalPostResolver`, `Post.IsExternal`, `WorkflowCommentThread`, `WorkflowCommentAudit`, `IReplyPlatformAdapter` for Bluesky and Meta Threads with automatic Graph API shortcode-to-mediaId resolution via `GetUserThreadsAsync`, `CommentEnrichmentService` with automated video/audio transcription via `ITranscriptExtractionService` and vision inspection, `CommentRankingService` 1-10 scoring/terminal closure, `ReplyGenerationService` leveraging draft `ChatToolExecutor` and `IChatTool`s including `TranscribeVideoTool`, `web_search`, `fetch_source`, `search_sources`, multi-turn tool loops with final-turn forced synthesis, `CommentThreadSummarizer` rolling summary cache, stop criteria enforcement)
- Background tasks & durable queue: api/SocialWorker.Api/Features/BackgroundTasks/ (PostgreSQL atomic queue with Allow-List worker filtering, PostgresBackgroundTaskQueue, workflow-reply-check-post, reply-thread-summary, scheduled-publishing, ScheduleEvaluationHostedService, BackgroundTaskRetentionService)
- Background task worker pool: api/SocialWorker.Api/Features/BackgroundTasks/Services/BackgroundTaskHostedService.cs (configurable worker workloads, dedicated transcription worker, general workers)
- Configuration & Overrides: api/SocialWorker.Api/Program.cs (mounted host override `./config/appsettings.json` -> `/app/config/appsettings.json:ro`)
- Auth/providers/accounts/users: api/SocialWorker.Api/Features/Auth/, Providers/, Accounts/, Users/ (TokenRefreshHostedService)
- API Key Subsystem: api/SocialWorker.Api/Infrastructure/Auth/ (ApiKeyService, ApiKeyAuthenticationHandler, SHA-256 hash storage)
- Model Context Protocol (MCP): api/SocialWorker.Api/Features/Mcp/ (JSON-RPC 2.0, SSE Transport `/api/mcp/sse`, Stateless `/api/mcp`, 66 tools in McpToolRegistry across 9 domains including media upload/templates/code rendering, schedule/queue management, lifecycle deletion, variants, video transcription, search, dryRun simulation, McpResourceRegistry; MCP-First operations policy in .agents/rules/mcp-first.md; complete documentation in docs/mcp_server.md)
- Markdown AST & Thread Document: api/SocialWorker.Api/Infrastructure/Markdown/ (Markdig CommonMark AST parser for segments, media, links, and facets)


### Frontend entry and state

- App shell & routing: web/src/App.tsx, web/src/components/Navigation/AppHeader.tsx (Workflows, Reply Agents, Outbox, Calendar, Drafts, Templates, Settings)
- Reply Agents Cockpit: web/src/components/ReplyAgents/ (ReplyAgentsPage top-level screen with Monitored Posts, Agents Library, Approval Inbox, Activity Stream, MonitorPostModal; ReplyAgentDetailPage deep management)
- Content Calendar & Views: web/src/components/Calendar/ (CalendarPage, MonthView, WeekView, DayView, QueueSidebar with Priority Badges, ScheduleManagerModal, SlotRuleFormModal, DraftPickerModal)
- Review Outbox Triage: web/src/components/Outbox/ReviewOutboxPage.tsx
- Settings Library: web/src/components/Settings/ (AccountTab, ConnectionsTab, BrandVoicesTab, FeedsTab, UsersTab, ProvidersTab)
- Workflows & Detail Dashboard: web/src/components/WorkflowsPanel/, web/src/components/WorkflowDetail/ (polymorphic sources: RSS/YouTube feeds, Workflow Highlights/Digests, WorkflowReplyAgentTab with agent selector and live audit triage)
- Drafts Library Page: web/src/components/Drafts/DraftsPage.tsx
- Full-width 2-pane Composer: web/src/components/Composer/ComposerPage.tsx
- Scoped Composer Draft Context: web/src/components/Composer/ComposerDraftContext.tsx (local draft, editor doc, autosave, scoped sources)
- Scoped Chat Provider: web/src/api/chat.tsx (scoped assistant-ui runtime seeded from and autosaved to draft.chatHistory)
- Navigation & Breadcrumbs: web/src/components/Navigation/
- Template Studio & WYSIWYG Editor: web/src/components/TemplateStudio/
- Draft library cache: web/src/store/draftStore.ts
- Global error catcher & crash safety: web/src/components/common/GlobalErrorToaster.tsx, web/src/components/common/ErrorBoundary.tsx
- API contract clients: web/src/api/ (schedules.ts, outbox.ts, drafts.ts, workflows.ts, replyAgents.ts)

### Tests

- API unit/integration tests (868 tests): api/SocialWorker.Api.Tests/
- Web tests (Vitest, 492 tests in 93 test files): web/src/**/*.test.ts, web/src/**/*.test.tsx
- Image Engine tests (Vitest, 14 tests in 3 test files): image-engine/tests/
- MCP & Stress test suites: scripts/mcp-stress-test-suite.mjs, scripts/test-mcp-e2e.mjs
- E2E (Playwright in Docker): e2e/ and scripts/run-e2e.sh

## Core user flow mental model

1. User opens/creates a draft (via Web UI or MCP server).
2. Chat and editor collaborate on thread content using CommonMark AST segmentation (`---`).
3. User attaches sources (URLs, YouTube, Instagram reels/posts, files); backend scrapes content, runs Whisper transcription for video audio, and extracts text.
4. Automation workflows poll RSS/YouTube/Instagram feeds and autonomously generate drafts citing research/articles.
5. Platform thread variants are created/updated with server-enforced validation rules (character limits, facets, single-card embed constraints).
6. Stage transitions are explicit and server-enforced.
7. Publish is allowed only when validation rules are satisfied (readiness + character count + AST syntax checks).
8. Successful publish writes post records and updates thread stage to Sent.

## Model Context Protocol (MCP) & Authentication

- Endpoint: `/api/mcp` (Stateless Streamable HTTP JSON-RPC 2.0), `/api/mcp/sse` (Server-Sent Events continuous stream), with aliases `/mcp` and `/mcp/sse`.
- Tools (66): Complete catalog of 66 domain tools covering drafts, platform variants, media assets/templates, sources & video transcription, feed discovery & polling, workflows, queue/scheduling operations, accounts, reply agents, and monitored posts.
- Resources: `socialworker://feeds`, `socialworker://workflows`, `socialworker://drafts/{id}`, `socialworker://audits/{workflowId}`.
- Standard MCP Auth (Claude Compatible):
  - RFC 9728 401 challenge emits `WWW-Authenticate: Bearer realm="mcp", resource_metadata="{baseUrl}/.well-known/oauth-protected-resource"`.
  - RFC 8414 server metadata at `/.well-known/oauth-authorization-server` and OpenID alias.
  - RFC 7591 Dynamic Client Registration at `POST /oauth/register`.
  - OAuth 2.1 with PKCE S256 (`GET /oauth/authorize` always requiring re-authentication with username/password without trusting frontend cookies, `POST /oauth/token` with 1-hour JWT + 30-day refresh token). Scopes supported: `mcp:tools`, `mcp:read`, `mcp:write`.
- Legacy/Static Auth: `ApiKeyAuthenticationHandler` validates SHA-256 hashed keys (`sw_live_...`, `X-API-Key`, `?api_key=...`) routed through ASP.NET Core `PolicyScheme` ("Composite"). Dev key `sw_live_dev_admin_key_000000000000` seeded in development.

## Runtime and commands that matter most

- Fast hot-reloading dev stack (Vite HMR + dotnet watch): ./scripts/run-dev.sh
- Bring up stack: docker compose up --build
- Bring up stack with GPU: ./scripts/redeploy.sh --gpu
- Safe refresh of running stack: ./scripts/redeploy.sh
- API build (tooling profile): docker compose --profile tooling run --rm dotnet build
- API tests (tooling profile): docker compose --profile tooling run --rm dotnet test ../SocialWorker.Api.Tests/SocialWorker.Api.Tests.csproj
- Web test (tooling profile): docker compose --profile tooling run --rm web-test
- Web check & build (tooling profile): docker compose --profile tooling run --rm web-test "npm run build && npm run typecheck" (or docker compose build web)
- Image Engine test (tooling profile): docker compose --profile tooling run --rm image-engine-test
- MCP stress test: docker compose --profile tooling run --rm -v "$(pwd):/repo" -w /repo -e API_URL="http://api:8080" web-test node scripts/mcp-stress-test-suite.mjs
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
