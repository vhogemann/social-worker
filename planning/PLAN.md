# social-worker roadmap

This file is the single source of truth for roadmap status and planning links.

## Principles

- Keep this file current; do not maintain a second roadmap elsewhere.
- Keep active roadmap items small in number and grouped by theme.
- Keep completed implementation plans in [archive](archive).
- Keep speculative or long-horizon ideas in [future](future).

## Current state

### Built and working

- Chat SSE streaming and tool execution
- Markdown editor, thread preview, draft CRUD, stage flow
- Sources, media upload, image/code rendering
- Source library search and cross-draft source linking
- YouTube transcript extraction via local transcriber service (Docker)
- Source processing status polling + retry action in UI
- Tabbed YouTube source preview (Video / Transcript)
- RSS/Atom feed discovery, subscription ingestion, and polling automation
- Feed panel and subscription management UI
- Reply target metadata, quick-reply resolution, and reply target card UI
- Background job queue consolidation for feed/source orchestration
- Platform variants and Bluesky publishing
- Auth, providers, brand voice prompts, settings UI

### Product position

- v1 is usable for draft composition and Bluesky publishing
- main near-term work is Bluesky threaded replies, quality, maintainability, and source/search/scraper reliability

### v1 close-out status (2026-07-14)

- Draft-switch chat race hardened by canceling in-flight runs before thread import/reset
- Chat convergence guard behavior covered by deterministic `ChatServiceTests`
- Chat/thread rendering regressions covered for tool activity cards, post-preview hygiene, and media URI rewrites
- API build/tests, web build/typecheck/tests, and e2e smoke verification pass in Docker
- Main stack startup verification passes via `docker compose up --build`

## Roadmap themes

### 1. Source ingestion and knowledge reuse
 
Status: `Completed`
 
Goals:
 
- reusable source library across drafts
- better source search and retrieval
- YouTube transcript ingestion and reuse
- higher-confidence web ingestion and source management
 
Tracked in:
 
- [archive/SOURCES_LIBRARY_AND_TRANSCRIPTS.md](archive/SOURCES_LIBRARY_AND_TRANSCRIPTS.md)
- [archive/BLUESKY_SOURCE_THREAD_CONTEXT.md](archive/BLUESKY_SOURCE_THREAD_CONTEXT.md)
 
### 2. Publishing and platform reach
 
Status: `Completed`
 
Goals:
 
- improve Bluesky post-publish workflows
- Thread reply / expansion for published Bluesky threads
 
Tracked in:
 
- [archive/THREADED_REPLIES.md](archive/THREADED_REPLIES.md)
 
Deferred to v3:
- Twitter/X publisher
- LinkedIn publisher
- Facebook publisher
- Instagram publisher

### 3. Quality and release confidence

Status: `Now`

Goals:

- raise API, frontend, and E2E coverage
- keep generated docs and release flows reliable
- reduce regression risk on chat/tools/publishing paths

Tracked in:

- [TEST_COVERAGE_CLOSEOUT.md](TEST_COVERAGE_CLOSEOUT.md)
- [TEST_PLAN.md](TEST_PLAN.md)
- [TECH_DEBT.md](TECH_DEBT.md)
- [future/E2E_TESTING.md](future/E2E_TESTING.md)

### 4. Maintainability and platform hardening

Status: `Completed`

Goals:

- pay down service/tooling debt
- simplify operational behavior and internal boundaries
- improve long-term maintainability before major scope expansion

Tracked in:

- [archive/TECH_DEBT.md](archive/TECH_DEBT.md)

Reference:

- [archive/REFACTORING_PLAN.md](archive/REFACTORING_PLAN.md)

### 5. Advanced agent runtime

Status: `Later`

Goals:

- sandboxed execution for richer tools
- tool promotion / retrieval systems
- more autonomous workspace evolution features

Reference docs:

- [future/PYTHON_SANDBOX.md](future/PYTHON_SANDBOX.md)
- [future/SELF_EVOLVING_ASSISTANT.md](future/SELF_EVOLVING_ASSISTANT.md)

### 6. External interfaces and expansion

Status: `Later`

Goals:

- lightweight remote/mobile interfaces
- future multi-user and team workflows
- downstream integrations outside the core web app

Reference docs:

- [future/TELEGRAM_INTEGRATION.md](future/TELEGRAM_INTEGRATION.md)

## Active planning documents
 
These should remain first-class and be kept current:
 
| File | Role |
|---|---|
| [TECH_DEBT.md](TECH_DEBT.md) | engineering cleanup and reliability backlog |
| [TEST_PLAN.md](TEST_PLAN.md) | test strategy and coverage backlog |
 
## Reference-only planning documents
 
### Future ideas
 
- [future/E2E_TESTING.md](future/E2E_TESTING.md)
- [future/PYTHON_SANDBOX.md](future/PYTHON_SANDBOX.md)
- [future/SELF_EVOLVING_ASSISTANT.md](future/SELF_EVOLVING_ASSISTANT.md)
- [future/TELEGRAM_INTEGRATION.md](future/TELEGRAM_INTEGRATION.md)
 
### Historical completed plans
 
- [archive/AUTHENTICATION.md](archive/AUTHENTICATION.md)
- [archive/TOOL_IMPROVEMENTS.md](archive/TOOL_IMPROVEMENTS.md)
- [archive/BRAND_VOICE_PROMPTS.md](archive/BRAND_VOICE_PROMPTS.md)
- [archive/CHAT_HISTORY_PERSISTENCE.md](archive/CHAT_HISTORY_PERSISTENCE.md)
- [archive/CHAT_SERVICE_REFACTORING.md](archive/CHAT_SERVICE_REFACTORING.md)
- [archive/IMAGE_UPLOADS.md](archive/IMAGE_UPLOADS.md)
- [archive/LLM_PROVIDERS.md](archive/LLM_PROVIDERS.md)
- [archive/MVP.md](archive/MVP.md)
- [archive/PLATFORM_VARIANTS.md](archive/PLATFORM_VARIANTS.md)
- [archive/SEARCH_TOOL.md](archive/SEARCH_TOOL.md)
- [archive/SOURCES_LIBRARY_AND_TRANSCRIPTS.md](archive/SOURCES_LIBRARY_AND_TRANSCRIPTS.md)
- [archive/THREAD_STAGES.md](archive/THREAD_STAGES.md)
- [archive/REFACTORING_PLAN.md](archive/REFACTORING_PLAN.md)
- [archive/TO_REFACTOR.md](archive/TO_REFACTOR.md)
- [RSS_AUTOMATION.md](RSS_AUTOMATION.md)
- [SOURCES_SERVICE_REFACTOR.md](SOURCES_SERVICE_REFACTOR.md)
- [archive/THREADED_REPLIES.md](archive/THREADED_REPLIES.md)
- [archive/BLUESKY_SOURCE_THREAD_CONTEXT.md](archive/BLUESKY_SOURCE_THREAD_CONTEXT.md)

### Retained only as historical context

- [BLUESKY.md](BLUESKY.md)
- [archive/UI-DISCOVERY.md](archive/UI-DISCOVERY.md)
- [archive/UI-LIBRARIES.md](archive/UI-LIBRARIES.md)

## Backlog summary
 
### Near-term
 
- stronger E2E and release confidence
- source / search / scraper reliability improvements
 
### Mid-term
 
- thread reordering and multi-draft workflow improvements
- scheduled publishing
 
### Long-term
 
- additional publishers beyond Bluesky (v3)
- Python sandbox and advanced tool runtime
- Telegram / remote interface ideas
- multi-user / team support
- analytics / inbox / broader operations tooling