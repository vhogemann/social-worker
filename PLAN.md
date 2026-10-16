# social-worker roadmap

This file is the single source of truth for roadmap status and planning links.

## Principles

- Keep this file current; do not maintain a second roadmap elsewhere.
- Keep active roadmap items small in number and grouped by theme.
- Keep completed implementation plans in [planning/archive](planning/archive).
- Keep speculative or long-horizon ideas in [planning/future](planning/future).

## Where we are now (2026-08-07)

### Product baseline

- Core drafting/chat workflow is stable and usable end-to-end.
- Bluesky publishing works in production flow.
- Source ingestion stack is broad: URL, file, YouTube transcript, feed automation.
- Frontend and API coverage sweeps were completed and moved to archive.

- Instagram Profile Sources via Meta Business Discovery API (Graph API client, credential provider, auto caller ID resolution, feed discovery & polling, setup guide): [planning/INSTAGRAM_PROFILE_SOURCES.md](planning/INSTAGRAM_PROFILE_SOURCES.md), [docs/INSTAGRAM_SETUP.md](docs/INSTAGRAM_SETUP.md)
- Twitter / X support (OAuth 2.0 PKCE, refresh token rotation, chained reply tweets, 280 weighted-char validation, t.co budgeting, live X thread preview): [planning/TWITTER_SUPPORT.md](planning/TWITTER_SUPPORT.md)
- Meta Threads support (OAuth lifecycle, multi-post thread publishing, carousel media, resilient resume, 500-char validation): [planning/THREADS_SUPPORT.md](planning/THREADS_SUPPORT.md)
- Content Calendar & Scheduled Publishing Engine (Cronos recurrence, slot window pacing, BackgroundTasks queue, Review Outbox, Tailwind-native calendar UI): [planning/SCHEDULING_CALENDAR.md](planning/SCHEDULING_CALENDAR.md)
- Drafts Page card grid, full-width 2-pane Composer, and workflow-grounded routing: [planning/archive/DRAFTS_PAGE_AND_WORKFLOW_ROUTING.md](planning/archive/DRAFTS_PAGE_AND_WORKFLOW_ROUTING.md)
- Durable PostgreSQL `SKIP LOCKED` task queue for horizontal scaling: [planning/archive/DURABLE_POSTGRES_QUEUE.md](planning/archive/DURABLE_POSTGRES_QUEUE.md)
- Image Engine microservice & template rendering delivery (Node.js 22 + Sharp + @napi-rs/canvas, .NET client, templates CRUD, LLM tools): [planning/IMAGE_ENGINE.md](planning/IMAGE_ENGINE.md)
- LinkedIn member publishing MVP (single-post contract, mapping, pipeline): [planning/LINKED_IN.md](planning/LINKED_IN.md)
- Feed automation delivery: [planning/archive/RSS_AUTOMATION.md](planning/archive/RSS_AUTOMATION.md)
- Sources service extraction and hardening: [planning/archive/SOURCES_SERVICE_REFACTOR.md](planning/archive/SOURCES_SERVICE_REFACTOR.md)
- Tech-debt closeout inventory: [planning/archive/TECH_DEBT_CLOSEOUT_2026-07-28.md](planning/archive/TECH_DEBT_CLOSEOUT_2026-07-28.md)
- Coverage closeout plan and tracker: [planning/archive/TEST_COVERAGE_CLOSEOUT.md](planning/archive/TEST_COVERAGE_CLOSEOUT.md), [planning/archive/TEST_PLAN_2026-07-28.md](planning/archive/TEST_PLAN_2026-07-28.md)

## Execution roadmap

### Now (next 2-4 weeks)

Status: `Active`

1. Reliability hardening for source/search/scraper pipeline.
2. E2E signal quality and release confidence (flake triage + stable smoke gates).
3. Tighten operational runbook quality (startup, verification, troubleshooting).
4. MCP Server capabilities enhancement & agent guidance: [planning/MCP_CAPABILITIES_ENHANCEMENT.md](planning/MCP_CAPABILITIES_ENHANCEMENT.md), [planning/MCP_GAPS_ROUND_2.md](planning/MCP_GAPS_ROUND_2.md)
5. Workflow Pausing & Archiving Lifecycle: [planning/WORKFLOW_PAUSE_AND_ARCHIVE.md](planning/WORKFLOW_PAUSE_AND_ARCHIVE.md)

Definition of done for this phase:

- No recurring ingestion regressions across URL/YouTube/feed flows.
- E2E smoke remains green on repeated Docker runs.
- Major failure modes have explicit tests and documented recovery paths.

### Next (after reliability gate)

Status: `Planned`

1. Meta platform integration: shared Meta OAuth + Instagram publisher + Facebook publisher (see [META_OAUTH.md](planning/META_OAUTH.md), [INSTAGRAM_SUPPORT.md](planning/INSTAGRAM_SUPPORT.md), [FACEBOOK_SUPPORT.md](planning/FACEBOOK_SUPPORT.md)).
2. Thread reordering and multi-draft workflow improvements.
3. Scheduled publishing.
4. Publishing UX refinements around review/confirm before send.

Scope guardrails:

- Keep Bluesky support operational and reliable, not feature-maximal.
- Do not evolve the web UI into a full-featured social client.

### Later (v3+)

Status: `Deferred`

1. Additional publishers: Twitter/X.
2. Advanced runtime ideas: sandbox/tool runtime evolution.
3. External interfaces and multi-user/team workflows.

## Active planning documents

These should remain first-class and be kept current:

| File | Role |
|---|---|
| [PLAN.md](PLAN.md) | canonical active roadmap and execution priorities |
| [planning/COMMERCIAL_STRATEGY_MASTER.md](planning/COMMERCIAL_STRATEGY_MASTER.md) | Master commercial strategy, competitive brief, running cost model, and launch roadmap |
| [planning/COMPETITIVE_BENCHMARK.md](planning/COMPETITIVE_BENCHMARK.md) | Competitive benchmark analysis vs. Postiz, Mixpost, Typefully, Taplio, and Buffer/Hootsuite |
| [planning/OPERATING_COST_ESTIMATE.md](planning/OPERATING_COST_ESTIMATE.md) | 1-year operational cost estimate and margin breakdown for independent launch (3-4 clients) |
| [planning/COMMERCIAL_EVALUATION.md](planning/COMMERCIAL_EVALUATION.md) | Commercial feasibility study for on-premises B2B enterprise sales and codebase/IP asset buyout |
| [planning/SCHEDULED_PUBLISHING.md](planning/SCHEDULED_PUBLISHING.md) | Scheduled publishing engine architecture, calendar slot windows, queue state model, and UI integration |
| [planning/scheduling/BACKEND_IMPLEMENTATION_PLAN.md](planning/scheduling/BACKEND_IMPLEMENTATION_PLAN.md) | Backend implementation plan: Cronos recurrence, slot pacing, SKIP LOCKED dispatcher, and outbox APIs |
| [planning/scheduling/FRONTEND_IMPLEMENTATION_PLAN.md](planning/scheduling/FRONTEND_IMPLEMENTATION_PLAN.md) | Frontend implementation plan: Tailwind-native CSS grid calendar, Review Outbox, and Composer integration |
| [planning/TWITTER_SUPPORT.md](planning/TWITTER_SUPPORT.md) | Twitter / X OAuth 2.0 PKCE connector, API v2 thread chaining, and validation specification |
| [planning/LINKED_IN.md](planning/LINKED_IN.md) | LinkedIn publishing contract, markdown mapping, and conversion pipeline (single-post-only scope) |
| [planning/linkedin/FRONTEND_INTEGRATION.md](planning/linkedin/FRONTEND_INTEGRATION.md) | LinkedIn frontend integration design, preview components, and validation rules |
| [planning/linkedin/OAUTH_INTEGRATION.md](planning/linkedin/OAUTH_INTEGRATION.md) | LinkedIn 3-legged OAuth code flow backend and frontend integration |
| [planning/linkedin/COMPANY_PAGES.md](planning/linkedin/COMPANY_PAGES.md) | LinkedIn Company Page (Organization) posting, ACL discovery, and multi-account publishing |
| [planning/DECOUPLED_ORCHESTRATION.md](planning/DECOUPLED_ORCHESTRATION.md) | Decoupled feed ingestion and multi-source/multi-destination orchestration workflows |
| [planning/FRONTEND_ORCHESTRATION.md](planning/FRONTEND_ORCHESTRATION.md) | Frontend UI integration for orchestrations dashboard, many-to-many feed mappings, and recent runs backlog |
| [planning/MULTI_ACCOUNT_SUPPORT.md](planning/MULTI_ACCOUNT_SUPPORT.md) | Multi-account support per platform across database constraints, account-first draft creation, and publishing |
| [planning/FACEBOOK_SUPPORT.md](planning/FACEBOOK_SUPPORT.md) | Facebook Pages OAuth integration, token exchange, and post/comment/media publishing pipeline |
| [planning/INSTAGRAM_SUPPORT.md](planning/INSTAGRAM_SUPPORT.md) | Instagram Professional/Creator publishing container pipeline, image validation, and OAuth integration |
| [planning/INSTAGRAM_PROFILE_SOURCES.md](planning/INSTAGRAM_PROFILE_SOURCES.md) | Instagram profile content ingestion via Meta Business Discovery API and RSSHub sidecar bridge |
| [planning/META_OAUTH.md](planning/META_OAUTH.md) | Shared Meta OAuth 2.0 infrastructure for Facebook Pages and Instagram Professional account discovery |
| [planning/IMAGE_ENGINE.md](planning/IMAGE_ENGINE.md) | Image Engine microservice (Sharp + @napi-rs/canvas), data-driven JSON layer tree templates, Instagram image composition, and partial SkiaSharp extraction |
| [planning/TEMPLATE_STUDIO.md](planning/TEMPLATE_STUDIO.md) | Template Studio UI, AI template generation, 1-shot auto-repair, and seed templates library |
| [planning/CLOUD_BLOB_STORAGE.md](planning/CLOUD_BLOB_STORAGE.md) | Pluggable S3-compatible Cloud Blob Storage (AWS S3, Cloudflare R2, MinIO, Local FileSystem) |
| [planning/KUBERNETES_DEPLOYMENT.md](planning/KUBERNETES_DEPLOYMENT.md) | Production Kubernetes deployment architecture (Kustomize, Helm, HPA, Ingress, GPU node scheduling) |
| [planning/DEMO_CLOUD_PROVIDER_RESEARCH.md](planning/DEMO_CLOUD_PROVIDER_RESEARCH.md) | Cloud provider cost & compute benchmark research for initial live demo deployment |
| [planning/INFRASTRUCTURE_GAPS_AND_HARDENING.md](planning/INFRASTRUCTURE_GAPS_AND_HARDENING.md) | Infrastructure gaps audit & production hardening plan (Health checks, Rate limiting, Data Protection, OAuth refresh, Backup) |
| [planning/MCP_GAPS_ROUND_2.md](planning/MCP_GAPS_ROUND_2.md) | MCP server gap analysis and enhancements: media tools, account selection, publishing data fixes, and queue lifecycle |
| [planning/FUNCTIONAL_TEST_HARNESS.md](planning/FUNCTIONAL_TEST_HARNESS.md) | Agent-controlled functional test harness for live stack & Bluesky ATProto out-of-band verification |
| [planning/DASHBOARD_OVERVIEW.md](planning/DASHBOARD_OVERVIEW.md) | Overview dashboard architecture, KPI metrics, velocity time-series graphs, and low-tax database index design |
| [planning/ENGAGEMENT_SYNC_QUEUE.md](planning/ENGAGEMENT_SYNC_QUEUE.md) | Unified multi-platform engagement sync queue, batching, tiered lookback windows, and background workers |





## Reference-only planning documents

### Future ideas

- [planning/future/E2E_TESTING.md](planning/future/E2E_TESTING.md)
- [planning/future/GRAPHQL_EVALUATION.md](planning/future/GRAPHQL_EVALUATION.md)
- [planning/future/IMAGE_ENGINE_EXTENSIONS.md](planning/future/IMAGE_ENGINE_EXTENSIONS.md)
- [planning/future/PYTHON_SANDBOX.md](planning/future/PYTHON_SANDBOX.md)
- [planning/future/SELF_EVOLVING_ASSISTANT.md](planning/future/SELF_EVOLVING_ASSISTANT.md)
- [planning/future/TELEGRAM_INTEGRATION.md](planning/future/TELEGRAM_INTEGRATION.md)

### Historical completed plans

- [planning/archive/DRAFTS_PAGE_AND_WORKFLOW_ROUTING.md](planning/archive/DRAFTS_PAGE_AND_WORKFLOW_ROUTING.md)
- [planning/archive/AUTHENTICATION.md](planning/archive/AUTHENTICATION.md)
- [planning/archive/TOOL_IMPROVEMENTS.md](planning/archive/TOOL_IMPROVEMENTS.md)
- [planning/archive/BRAND_VOICE_PROMPTS.md](planning/archive/BRAND_VOICE_PROMPTS.md)
- [planning/archive/CHAT_HISTORY_PERSISTENCE.md](planning/archive/CHAT_HISTORY_PERSISTENCE.md)
- [planning/archive/CHAT_SERVICE_REFACTORING.md](planning/archive/CHAT_SERVICE_REFACTORING.md)
- [planning/archive/IMAGE_UPLOADS.md](planning/archive/IMAGE_UPLOADS.md)
- [planning/archive/LLM_PROVIDERS.md](planning/archive/LLM_PROVIDERS.md)
- [planning/archive/MVP.md](planning/archive/MVP.md)
- [planning/archive/PLATFORM_VARIANTS.md](planning/archive/PLATFORM_VARIANTS.md)
- [planning/archive/SEARCH_TOOL.md](planning/archive/SEARCH_TOOL.md)
- [planning/archive/SOURCES_LIBRARY_AND_TRANSCRIPTS.md](planning/archive/SOURCES_LIBRARY_AND_TRANSCRIPTS.md)
- [planning/archive/THREAD_STAGES.md](planning/archive/THREAD_STAGES.md)
- [planning/archive/REFACTORING_PLAN.md](planning/archive/REFACTORING_PLAN.md)
- [planning/archive/TO_REFACTOR.md](planning/archive/TO_REFACTOR.md)
- [planning/archive/RSS_AUTOMATION.md](planning/archive/RSS_AUTOMATION.md)
- [planning/archive/SOURCES_SERVICE_REFACTOR.md](planning/archive/SOURCES_SERVICE_REFACTOR.md)
- [planning/archive/TECH_DEBT.md](planning/archive/TECH_DEBT.md)
- [planning/archive/TECH_DEBT_CLOSEOUT_2026-07-28.md](planning/archive/TECH_DEBT_CLOSEOUT_2026-07-28.md)
- [planning/archive/TEST_COVERAGE_CLOSEOUT.md](planning/archive/TEST_COVERAGE_CLOSEOUT.md)
- [planning/archive/TEST_PLAN_2026-07-28.md](planning/archive/TEST_PLAN_2026-07-28.md)
- [planning/archive/THREADED_REPLIES.md](planning/archive/THREADED_REPLIES.md)
- [planning/archive/BLUESKY_SOURCE_THREAD_CONTEXT.md](planning/archive/BLUESKY_SOURCE_THREAD_CONTEXT.md)
- [planning/archive/OLLAMA_FEEDBACK.md](planning/archive/OLLAMA_FEEDBACK.md)
- [planning/archive/BLUESKY.md](planning/archive/BLUESKY.md)

### Retained only as historical context

- [planning/archive/UI-DISCOVERY.md](planning/archive/UI-DISCOVERY.md)
- [planning/archive/UI-LIBRARIES.md](planning/archive/UI-LIBRARIES.md)
