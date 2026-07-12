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
- **Local HTTPS**: Caddy + mkcert. One host-side bootstrap script generates certs.
- **v1 auth**: single-user, no auth. Platform tokens stored encrypted in Postgres (AES with env key).

## Running locally

Everything runs under Docker. Only host prerequisites: Docker (with Compose v2) and `mkcert` (plus `nss`/`firefox` extras only if using Firefox).

```
# one-time cert bootstrap
./scripts/bootstrap.sh

# CPU (Mac or Linux without GPU)
docker compose --profile local-llm up --build

# Linux + NVidia GPU (requires nvidia-container-toolkit on host)
docker compose --profile local-llm -f docker-compose.yml -f docker-compose.gpu.yml up --build
```

Domains (after bootstrap):
- `https://social-worker.localtest` — app
- `https://api.social-worker.localtest` — API
- `https://db.social-worker.localtest` — Adminer

Pull a local model: `docker exec social-worker-ollama ollama pull llama3.1`.

## Layout

```
social-worker/
├─ docker-compose.yml              # base stack (CPU)
├─ docker-compose.gpu.yml          # NVidia override for ollama
├─ .env / .env.example
├─ scripts/bootstrap.sh            # mkcert cert generation
├─ proxy/                          # Caddyfile + generated certs (gitignored)
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
- **Secrets**: never commit `.env`, `proxy/certs/`, or any API keys. `.gitignore` must cover these.
- **TypeScript types** for API responses are hand-synced in `web/src/api/`. If the API contract changes, update both sides. (OpenAPI codegen is a later option.)
- **EF migrations**: generate with `dotnet ef migrations add <Name>` inside the `api` container; commit the migration files. The migrator service applies them on `up`.
- **New platform publishers** implement `IPublisher` in `api/SocialWorker.Api/Features/Publishing/`. `BlueskyPublisher` is the reference; others return `NotImplemented` + an auth URL until implemented.
- **Stage transitions** are server-enforced: the model calls `set_stage`, but the server only applies it after the UI records user approval. `publish` is rejected unless `Stage=Ready` and the target platform's variant is confirmed.
- **Implementation plans**: saved as `*.md` files in the `docs/` directory (e.g. `docs/IMAGE_UPLOADS.md`, `docs/AUTHENTICATION.md`). Each plan must be linked from `PLAN.md` under the relevant section. This is the source of truth for tracking planned and completed work.
- **TDD / Test Coverage**: Nothing is done until it is covered by tests. All new features, services, endpoints, and tools must have accompanying unit tests under `api/SocialWorker.Api.Tests/`.

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

Concretely for API changes:

1. `docker compose --profile tooling run --rm dotnet build` — confirm baseline compiles
2. `docker compose --profile tooling run --rm dotnet test SocialWorker.Api.Tests/SocialWorker.Api.Tests.csproj` — confirm baseline tests pass
3. Make the change
4. `docker compose --profile tooling run --rm dotnet build` — confirm change compiles
5. `docker compose --profile tooling run --rm dotnet test SocialWorker.Api.Tests/SocialWorker.Api.Tests.csproj` — confirm tests still pass

This workflow applies to all changes, not just new features. The `dotnet` service is defined under `profiles: [tooling]` and never starts with `docker compose up` — it is only used via `docker compose --profile tooling run --rm`.

## Out of scope for v1

- Multi-user / auth.
- Scheduling/calendar.
- Analytics/inbox.
- Image generation (attach files only).
- Twitter/LinkedIn/Facebook/Instagram publishing (stubbed).
