# MENTAL_MODEL.md — social-worker

## Purpose

This document is the fast orientation map for agents and contributors. It captures how the system is structured, where key behavior lives, and how to validate changes.

Read this first, then use AGENTS.md for stricter policy and workflow rules.

## Product in one paragraph

social-worker is a local-first, Docker-only assistant for drafting and publishing social media threads. The user iterates in a chat + editor workflow, adds sources/media, formats per platform, explicitly confirms stage transitions, and publishes. Bluesky is production-ready in v1; other publishers are stubs.

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
- Feed automation: api/SocialWorker.Api/Features/Feeds/
- Feed ingestion queue worker: api/SocialWorker.Api/Features/Feeds/Services/FeedIngestionQueueHostedService.cs
- Auth/providers/accounts/users: api/SocialWorker.Api/Features/Auth/, Providers/, Accounts/, Users/

### Frontend entry and state

- App shell + routes + panel layout: web/src/App.tsx
- Draft state/orchestration: web/src/store/draftStore.ts
- Chat state and activity cards: web/src/store/chatStore.ts
- API contract clients: web/src/api/

### Tests

- API unit/integration tests: api/SocialWorker.Api.Tests/
- Web tests (Vitest): web/src/**/*.test.ts
- E2E (Playwright in Docker): e2e/ and scripts/run-e2e.sh

## Core user flow mental model

1. User opens/creates a draft.
2. Chat and editor collaborate on thread content.
3. User adds sources/media; backend stores and links them.
4. Platform thread variants are created/updated.
5. Stage transitions are explicit and server-enforced.
6. Publish is allowed only when rules are satisfied (for example readiness + platform validations).
7. Successful publish writes post records and updates thread stage to Sent.

## Runtime and commands that matter most

- Bring up stack: docker compose up --build
- Safe refresh of running stack: ./scripts/redeploy.sh
- API build (tooling profile): docker compose --profile tooling run --rm dotnet build
- API test (tooling profile): docker compose --profile tooling run --rm dotnet test SocialWorker.Api.Tests/SocialWorker.Api.Tests.csproj
- Web check: docker compose exec web sh -c "npm run build && npm run typecheck"
- E2E: ./scripts/run-e2e.sh
- SQL against Postgres: ./scripts/sql.sh -c "select now();"

## Feed queue behavior

- Feed polling discovers new feed items and persists them to a DB-backed queue table.
- Queue processing is handled by a hosted service with retry/backoff (max attempts + next-at scheduling).
- Queue backlog can be inspected and managed (manual retry/delete) from the Feeds UI.

## Active roadmap center of gravity

- Canonical roadmap index: planning/PLAN.md
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
