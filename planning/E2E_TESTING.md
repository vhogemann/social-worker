# E2E_TESTING.md — Automated End-to-End Testing Strategy

## Tool

**Playwright** over Cypress. Rationale:

- Official `mcr.microsoft.com/playwright` Docker image ships Node + Chromium/Firefox/WebKit — no Xvfb, no extra deps
- Native TypeScript, fits the existing Vite toolchain naturally
- Built-in network interception (stub API calls, wait for SSE responses) — useful for testing LLM-backed endpoints
- API testing baked in — can call `api.social-worker.localtest` directly from tests without a browser
- Auto-waiting semantics reduce flakiness vs Cypress manual chains
- Parallel test execution by default

## Structure: standalone `e2e/` directory

```
e2e/
├── Dockerfile
├── package.json
├── tsconfig.json
├── playwright.config.ts
├── fixtures/
│   └── drafts.ts                  # typed fixture data (factory functions)
├── tests/
│   ├── smoke.spec.ts              # health checks, TLS smoke
│   ├── drafts/
│   │   ├── create.spec.ts
│   │   ├── edit.spec.ts
│   │   └── delete.spec.ts
│   ├── sources/
│   │   ├── add-url.spec.ts
│   │   └── add-file.spec.ts
│   ├── chat/
│   │   └── streaming.spec.ts
│   ├── publishing/
│   │   └── bluesky-publish.spec.ts
│   └── helpers/
│       ├── api.ts                 # typed fetch wrapper (uses internal http://web:80)
│       ├── seed.ts                # API-based data seeding
│       ├── cleanup.ts             # teardown helpers
│       └── wait-for-app.ts        # readiness check before tests
├── global-setup.ts                # seed + wait logic
└── global-teardown.ts             # cleanup artifacts
```

Not nested in `web/` because:
- Different dependency set (Playwright, not Vitest/jsdom)
- Targets running containers, not Vite dev server
- Doesn't bloat the web image with browser binaries

## Dockerfile

```dockerfile
FROM mcr.microsoft.com/playwright:v1.52.0-jammy
WORKDIR /e2e
COPY package.json package-lock.json* ./
RUN npm ci
COPY . .
CMD ["npx", "playwright", "test"]
```

No multi-stage needed — the Playwright image ships Node + browsers.

## Compose integration: profile `e2e`

Add to `docker-compose.yml`:

```yaml
e2e:
  profiles: ["e2e"]
  build:
    context: ./e2e
    dockerfile: Dockerfile
  environment:
    BASE_URL: http://web:80
  depends_on:
    api:
      condition: service_started
    web:
      condition: service_started
  volumes:
    - ./e2e/reports:/e2e/reports
```

`profiles: ["e2e"]` keeps it invisible to daily `docker compose up` — zero impact on dev workflow.

## HTTPS / Certs

All tests use internal `http://web:80` (nginx serves the app internally, same origin proxy to API). No TLS needed inside the Docker network — the app runs on `http://localhost:8100` on the host.

## Data Seeding

**Hybrid: API calls from test setup, not direct DB.**

- `global-setup.ts` calls `POST /__tests/reset` to clean state
- Individual tests seed via helper functions: `POST /api/drafts`, `POST /api/sources`, etc.
- Add a `POST /api/__tests/reset` endpoint to the API (under `#if DEBUG` / `if (app.Environment.IsDevelopment())`) that truncates Drafts, Sources, PlatformVariants, Posts while keeping Accounts

Why not direct DB:
- DB connection would need a separate compose network or exposed port — breaks the "only `api` talks to `db`" rule
- Raw inserts bypass domain logic (validation, encryption, LLM state machine transitions)
- API seeding doubles as an API smoke test

## Running

```bash
# full suite (build + run + exit with test exit code)
docker compose --profile e2e up --build --abort-on-container-exit --exit-code-from e2e

# re-run without rebuild
docker compose --profile e2e run --rm e2e npx playwright test --headed

# single file
docker compose --profile e2e run --rm e2e npx playwright test tests/drafts/create.spec.ts
```

## CI (GitHub Actions)

```yaml
- run: docker compose --profile e2e up --build --abort-on-container-exit --exit-code-from e2e
- uses: actions/upload-artifact@v4
  with:
    name: playwright-report
    path: e2e/reports/
```

Single command, no matrix, no separate runner setup — the Playwright image has everything.

## What to test (v1 scope)

| Priority | Flow | Notes |
|---|---|---|
| P0 | Smoke — app loads, API responds | Health check |
| P0 | Create draft from UI | Fill title, submit, appears in sidebar |
| P0 | Chat streams response | SSE visible in chat panel, tool calls execute |
| P1 | Add source (URL) to draft | Paste URL, confirm it appears and is cached |
| P1 | Adapt draft for platform (Bluesky) | Click Adapt, see variants generated |
| P1 | Publish to Bluesky (mock) | Confirm the post flow — actual publishing requires real creds |
| P2 | Edit draft in CodeMirror | Type in editor, save, reload |
| P2 | Delete draft | Confirm list updates |
| P2 | Add media to draft | Upload file, see it in the segment |
| P3 | Rename draft | Inline rename |
| P3 | Archive / restore draft | Toggle archive state |

## Out of scope (v1)

- Cross-browser testing (Chromium-only is fine for v1)
- Visual regression / Percy-style snapshot diffing
- Accessibility audit automation
- Performance / Lighthouse budgets
- Multi-user / auth flows (single-user only)
- Twitter / LinkedIn / FB / Instagram publishing (stubbed)
