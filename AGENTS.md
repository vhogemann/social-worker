# AGENTS.md — social-worker

## Project

**social-worker** is a local-first, Docker-only multi-modal assistant for composing and publishing social media **threads**. A chat/composer UI lets the user draft a thread with LLM help, attach sources and media, adapt to each target platform, confirm, and publish. v1 ships Bluesky publishing working end-to-end; Twitter, LinkedIn, Facebook, Instagram are stubbed.

## Core workflow

```
rough draft → add sources → iterate → improve → format per-platform → confirm → post
```

Thread-first: a draft is an ordered thread of segments. Stages are explicit and user-approved — the model proposes transitions but cannot self-advance or publish without confirmation.

## Stack

- **Frontend** (`web/`): Vite + React + TypeScript + Tailwind. State via zustand, server state via @tanstack/react-query, SSE chat via native `EventSource`. No Vercel AI SDK.
- **Backend** (`api/`): .NET 10 ASP.NET Core Minimal API + Entity Framework Core + Postgres.
- **LLM**: `Microsoft.Extensions.AI` + `OpenAI` .NET SDK with configurable `BaseUrl` to support OpenAI, OpenRouter, and Ollama (all OpenAI-compatible).
- **DB**: Postgres 16. Migrations run automatically via a one-shot `migrator` compose service on `docker compose up`.
- **Stack**: .NET 10, ASP.NET Core Minimal API, EF Core + Postgres 16, Vite + React + TypeScript + Tailwind, SkiaSharp, SearXNG.
- **Hosting**: Docker Compose only. NGINX serves the frontend and proxies `/api/` to the backend.
- **v1 auth**: single-user, no auth. Platform tokens stored encrypted in Postgres (AES with env key).

## Running locally

Everything runs under Docker. Only host prerequisite: Docker (with Compose v2).

```
docker compose up --build
```

- `http://localhost:8100` — app
- `http://localhost:8101` — API

## Layout

```
social-worker/
├─ docker-compose.yml              # base stack (CPU)
├─ docker-compose.gpu.yml          # NVidia override for ollama
├─ .env / .env.example
├─ scripts/bootstrap.sh            # mkcert cert generation
├─ proxy/                          # Caddyfile + generated certs (gitignored, unused — app runs on localhost:8100)
├─ api/                            # .NET 10 Minimal API + EF Core
│  └─ SocialWorker.Api/
└─ web/                            # Vite + React + TS + Tailwind
   └─ src/
```

## Data model

- `Account` — platform credentials (encrypted).
- `Draft` — has a `Stage` (RoughDraft, Sourcing, Refining, Formatting, Ready, Sent) and `Status`.
- `ThreadSegment` — ordered content + optional media; one segment = one post in the thread.
- `Source` — `Url` (fetched + cached), `File` (text extracted), `Note` (free-text), or reference image.
- `PlatformVariant` — per-platform adapted version of the thread.
- `Post` — one row per actually-published segment per platform.
- `MediaAsset`, `BrandVoicePrompt` — supporting.

## Conventions

- **No comments in code** unless asked.
- **No emojis** in code or commits.
- **Mimic existing style** when adding to a file; check neighbors first.
- **Do not commit** unless explicitly asked.
- **Secrets**: never commit `.env`, `proxy/certs/` (if present), or any API keys. `.gitignore` must cover these.
- **TypeScript types** for API responses are hand-synced in `web/src/api/`. If the API contract changes, update both sides. (OpenAPI codegen is a later option.)
- **Frontend testability**: build components with a forward look toward testing. Stable anchors such as `data-testid`, explicit labels, and other durable selectors are allowed when they improve e2e/unit test readability and robustness.
- **EF migrations**: generate with `dotnet ef migrations add <Name>` inside the `api` container; commit the migration files. The migrator service applies them on `up`.
- **New platform publishers** implement `IPublisher` in `api/SocialWorker.Api/Features/Publishing/`. `BlueskyPublisher` is the reference; others return `NotImplemented` + an auth URL until implemented.
- **Stage transitions** are server-enforced: the model calls `set_stage`, but the server only applies it after the UI records user approval. `publish` is rejected unless `Stage=Ready` and the target platform's variant is confirmed.
- **Implementation plans**: saved as `*.md` files in `planning/` and its subdirectories (for example `planning/archive/IMAGE_UPLOADS.md`, `planning/future/PYTHON_SANDBOX.md`). Each active or reference plan must be linked from `planning/PLAN.md` under the relevant section. This is the source of truth for tracking planned and completed work.
- **TDD / Test Coverage**: Nothing is done until it is covered by tests. All new features, services, endpoints, and tools must have accompanying unit tests under `api/SocialWorker.Api.Tests/`.

### Refactoring criteria for agents

When performing refactors, apply these criteria unless the task explicitly says otherwise:

- **Single responsibility split**: if one class handles API calls + content parsing + persistence + orchestration, split into focused services.
- **Thin orchestrator pattern**: keep top-level feature classes (for example, publishers/tools/endpoints) as coordinators that delegate to extracted services.
- **Typed models over dynamic JSON**: do not use `JsonObject`, `JsonNode`, or `JsonArray` in feature logic when a stable shape exists. Create explicit request/response/domain models instead.
- **Model extraction**: move non-trivial nested/anonymous payload shapes into named model files under the feature folder.
- **Behavior parity first**: preserve external behavior and wire-level payload shapes while refactoring internals.
- **No swallowed exceptions**: if catching `Exception`, log with `LogError` unless explicitly handling a safe best-effort path.
- **Cross-cutting rule centralization**: if the same validation/normalization logic appears in multiple endpoints/tools, extract to shared helper/service.
- **Bounded public constructors**: allow optional dependency injection overrides only when needed for testability; keep default wiring straightforward.
- **Tests required for refactors**: update/add tests for changed responsibilities and serialization contracts before considering refactor complete.

## LLM configuration

Set in `.env`. The API reads these at startup:

```
LLM__Provider=OpenRouter            # OpenAI | OpenRouter | Ollama
LLM__BaseUrl=https://openrouter.ai/api/v1
LLM__ApiKey=...
LLM__Model=anthropic/claude-3.5-sonnet
```

For Ollama, set `LLM__Provider=Ollama`, `LLM__BaseUrl=http://ollama:11434/v1`, `LLM__ApiKey=dummy`, `LLM__Model=llama3.1`, and enable the `local-llm` compose profile.

## Verification

No mandated test framework yet. When adding tests:
- **API**: xUnit under `api/SocialWorker.Api.Tests/`. Run via `dotnet test` inside the dotnet container.
- **Web**: Vitest if/when added.

**Builds and checks must run inside Docker, not on the host.** Do not invoke `dotnet`, `npm`, `npx`, or `tsc` directly on the host machine. The host may not have the required SDK/runtime versions and the dev environment must match prod. Use the compose images instead:

- API build: `docker compose --profile tooling run --rm dotnet build`
- API test: `docker compose --profile tooling run --rm dotnet test SocialWorker.Api.Tests/SocialWorker.Api.Tests.csproj`
- API build + test: `docker compose --profile tooling run --rm dotnet build && docker compose --profile tooling run --rm dotnet test SocialWorker.Api.Tests/SocialWorker.Api.Tests.csproj`
- Web: `docker compose exec web sh -c "npm run build && npm run typecheck"`. If the `web` service isn't running, use `docker compose run --rm web sh -c "npm run build && npm run typecheck"`. (The web runtime image is nginx-based; for build-only checks use `docker compose build web`.)
- To generate EF migrations: `docker compose --profile tooling run --rm dotnet ef migrations add <Name>` (the `dotnet-ef` tool is installed in the api Dockerfile build stage; for first use in the `dotnet` service run `dotnet tool install --global dotnet-ef` then ensure `PATH="$PATH:/root/.dotnet/tools"`).
- `docker compose up --build` is the canonical way to build and run everything.

## Change workflow (mandatory)

Before making any changes, establish a baseline by running the build and tests inside Docker. Then make changes, then re-run build and tests to verify. Do not move on to the next step until the build passes and all tests succeed.

Definition of done:

- It is not done just because unit tests or image builds pass.
- Before declaring work complete, rebuild the affected container images and start the relevant services with Docker Compose.
- Confirm the containers actually come up cleanly and that startup-time failures (migration issues, missing native libraries, misconfigured env vars, bad runtime wiring) are resolved.
- For stack-affecting changes, `docker compose up --build` or the project redeploy helper is required as the final verification step.

Concretely for API changes:

1. `docker compose --profile tooling run --rm dotnet build` — confirm baseline compiles
2. `docker compose --profile tooling run --rm dotnet test SocialWorker.Api.Tests/SocialWorker.Api.Tests.csproj` — confirm baseline tests pass
3. Make the change
4. `docker compose --profile tooling run --rm dotnet build` — confirm change compiles
5. `docker compose --profile tooling run --rm dotnet test SocialWorker.Api.Tests/SocialWorker.Api.Tests.csproj` — confirm tests still pass

This workflow applies to all changes, not just new features. The `dotnet` service is defined under `profiles: [tooling]` and never starts with `docker compose up` — it is only used via `docker compose --profile tooling run --rm`.

## E2E tests

E2E tests live in `e2e/` and use Playwright with Docker.

Use the dedicated e2e compose stack so tests do not collide with the main dev stack on ports 8100/8101. The e2e stack publishes web/api on 8200/8201.

For deterministic, LLM-free responses in e2e, run with `DEMO_LLM_PROFILE=getting-started` so the API uses the `DemoLlmAdapter` scenario profile.

Important: keep this variable scoped to e2e commands only. Do not leave `DEMO_LLM_PROFILE` set globally in your shell when doing normal local development.

Preferred helper:

```bash
# run full suite with isolated stack lifecycle
./scripts/run-e2e.sh

# run a single test file
./scripts/run-e2e.sh tests/chat.spec.ts

# keep stack up for debugging
./scripts/run-e2e.sh --keep-up tests/chat.spec.ts
```

```bash
# run the full suite
DEMO_LLM_PROFILE=getting-started docker compose -f docker-compose.e2e.yml run --rm e2e npx playwright test

# run a single test file
DEMO_LLM_PROFILE=getting-started docker compose -f docker-compose.e2e.yml run --rm e2e npx playwright test tests/chat.spec.ts

# regenerate the getting-started guide
docker compose -f docker-compose.e2e.yml build e2e
DEMO_LLM_PROFILE=getting-started docker compose -f docker-compose.e2e.yml run --rm e2e npx playwright test tests/generate-getting-started.spec.ts
cp e2e/output/GETTING_STARTED.md GETTING_STARTED.md

# re-run without rebuilding
DEMO_LLM_PROFILE=getting-started docker compose -f docker-compose.e2e.yml run --rm e2e npx playwright test
```

## Out of scope for v1

- Multi-user / auth.
- Scheduling/calendar.
- Analytics/inbox.
- Image generation (attach files only).
- Twitter/LinkedIn/Facebook/Instagram publishing (stubbed).
