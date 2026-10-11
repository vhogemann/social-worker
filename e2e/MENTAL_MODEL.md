# MENTAL_MODEL.md — e2e (Playwright End-to-End Test Suite)

## Purpose

This document outlines the Playwright test suite, isolated Docker test stack, deterministic LLM simulation harness, and test execution procedures for end-to-end validation.

## Tech Stack

- **Testing Framework**: Playwright Test (`@playwright/test` v1.62).
- **Language & Runtime**: TypeScript with Node.js in the `mcr.microsoft.com/playwright` Docker image.
- **Isolated Stack**: `docker-compose.e2e.yml` mapping Web to port 8200 and API to port 8201 to avoid colliding with local dev services (8100/8101).
- **Deterministic Mock LLM**: `DEMO_LLM_PROFILE=getting-started` replaces third-party LLM calls with deterministic scenario responses.

## Architecture & Layout

```
e2e/
├── Dockerfile                         # Playwright container definition
├── playwright.config.ts               # Base URL (http://web:80), retry, timeout, and reporter config
├── helpers/
│   ├── api.ts                         # Direct REST API client for test fixtures
│   ├── auth.ts                        # Session authentication helper and token injector
│   ├── cleanup.ts                     # Database and storage cleanup between test runs
│   ├── seed.ts                        # Seed test data (drafts, platforms, sample feeds)
│   └── wait-for-app.ts                # Polls web/api health before test execution begins
├── reports/                           # Generated Playwright HTML reports
├── screenshots/                       # Visual failure captures
└── tests/                             # End-to-end test specifications
    ├── smoke.spec.ts                  # App shell rendering and base route health
    ├── auth.spec.ts                   # Login, session persistence, and logout
    ├── api.spec.ts                    # Backend health endpoints and API availability
    ├── chat.spec.ts                   # 2-pane Composer chat messaging and SSE stream rendering
    ├── drafts.spec.ts                 # Draft creation, CommonMark editing, and stage transitions
    ├── settings.spec.ts               # Platform connection toggles and settings management
    ├── templates.spec.ts              # Template Studio canvas rendering and layer controls
    ├── workflows.spec.ts              # Feed automation creation, polling, and draft generation
    ├── feeds-logs.spec.ts             # Feed subscription status and execution log viewer
    └── publishing.spec.ts             # Confirmation gates and multi-platform publishing flow
```

## Deterministic LLM Mocking

E2E tests must never depend on live external LLM APIs (OpenAI / OpenRouter). The e2e compose configuration injects:
```bash
DEMO_LLM_PROFILE=getting-started
```
This forces `DemoLlmAdapter` inside the .NET API to return predictable responses for chat prompts, segment breakdowns, and workflow triggers without network latency, rate limits, or API costs.

## Running Tests

### 1. Preferred Runner Script

The `./scripts/run-e2e.sh` script manages the entire lifecycle (spin up isolated stack, wait for health gates, execute tests, capture reports, and tear down):

```bash
# Run the entire E2E suite
./scripts/run-e2e.sh

# Run a single test file
./scripts/run-e2e.sh tests/chat.spec.ts

# Keep stack up after tests for debugging
./scripts/run-e2e.sh --keep-up tests/drafts.spec.ts
```

### 2. Manual Docker Compose Invocation

```bash
# Execute tests directly against existing e2e stack
DEMO_LLM_PROFILE=getting-started docker compose -f docker-compose.e2e.yml run --rm e2e npx playwright test tests/smoke.spec.ts
```
