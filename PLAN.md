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

### Recently closed and archived

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

Definition of done for this phase:

- No recurring ingestion regressions across URL/YouTube/feed flows.
- E2E smoke remains green on repeated Docker runs.
- Major failure modes have explicit tests and documented recovery paths.

### Next (after reliability gate)

Status: `Planned`

1. Thread reordering and multi-draft workflow improvements.
2. Scheduled publishing.
3. Publishing UX refinements around review/confirm before send.

Scope guardrails:

- Keep Bluesky support operational and reliable, not feature-maximal.
- Do not evolve the web UI into a full-featured social client.

### Later (v3+)

Status: `Deferred`

1. Additional publishers: Twitter/X, LinkedIn, Facebook, Instagram.
2. Advanced runtime ideas: sandbox/tool runtime evolution.
3. External interfaces and multi-user/team workflows.

## Active planning documents

These should remain first-class and be kept current:

| File | Role |
|---|---|
| [PLAN.md](PLAN.md) | canonical active roadmap and execution priorities |

## Reference-only planning documents

### Future ideas

- [planning/future/E2E_TESTING.md](planning/future/E2E_TESTING.md)
- [planning/future/PYTHON_SANDBOX.md](planning/future/PYTHON_SANDBOX.md)
- [planning/future/SELF_EVOLVING_ASSISTANT.md](planning/future/SELF_EVOLVING_ASSISTANT.md)
- [planning/future/TELEGRAM_INTEGRATION.md](planning/future/TELEGRAM_INTEGRATION.md)

### Historical completed plans

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
