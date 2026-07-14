# social-worker 🛠️

A local-first, Docker-only multi-modal assistant for composing and publishing social media **threads**. 

Designed to support creators with LLM assistance, `social-worker` lets you draft threads, attach sources and media, adapt variants to multiple platforms, and publish. Version 1 ships with end-to-end publishing support for Bluesky, with stubs ready for Twitter, LinkedIn, Facebook, and Instagram.

## Badges

### Repository

[![API CI Workflow](https://img.shields.io/github/actions/workflow/status/vhogemann/social-worker/api-ci.yml?branch=main&label=API%20CI&logo=githubactions)](https://github.com/vhogemann/social-worker/actions/workflows/api-ci.yml)
[![Web CI Workflow](https://img.shields.io/github/actions/workflow/status/vhogemann/social-worker/web-ci.yml?branch=main&label=Web%20CI&logo=githubactions)](https://github.com/vhogemann/social-worker/actions/workflows/web-ci.yml)
[![Release Workflow](https://img.shields.io/github/actions/workflow/status/vhogemann/social-worker/release.yml?label=release&logo=githubactions)](https://github.com/vhogemann/social-worker/actions/workflows/release.yml)
[![GitHub Stars](https://img.shields.io/github/stars/vhogemann/social-worker?logo=github)](https://github.com/vhogemann/social-worker/stargazers)
[![GitHub Forks](https://img.shields.io/github/forks/vhogemann/social-worker?logo=github)](https://github.com/vhogemann/social-worker/forks)
[![GitHub Watchers](https://img.shields.io/github/watchers/vhogemann/social-worker?logo=github)](https://github.com/vhogemann/social-worker/watchers)
[![GitHub Issues](https://img.shields.io/github/issues/vhogemann/social-worker?logo=github)](https://github.com/vhogemann/social-worker/issues)
[![GitHub Closed Issues](https://img.shields.io/github/issues-closed/vhogemann/social-worker?logo=github)](https://github.com/vhogemann/social-worker/issues?q=is%3Aissue%20is%3Aclosed)
[![GitHub Pull Requests](https://img.shields.io/github/issues-pr/vhogemann/social-worker?logo=github)](https://github.com/vhogemann/social-worker/pulls)
[![GitHub Closed Pull Requests](https://img.shields.io/github/issues-pr-closed/vhogemann/social-worker?logo=github)](https://github.com/vhogemann/social-worker/pulls?q=is%3Apr%20is%3Aclosed)
[![GitHub Discussions](https://img.shields.io/github/discussions/vhogemann/social-worker?logo=github)](https://github.com/vhogemann/social-worker/discussions)
[![GitHub License](https://img.shields.io/github/license/vhogemann/social-worker?logo=github)](https://github.com/vhogemann/social-worker/blob/main/LICENSE)
[![GitHub Last Commit](https://img.shields.io/github/last-commit/vhogemann/social-worker?logo=github)](https://github.com/vhogemann/social-worker/commits/main)
[![GitHub Commit Activity (Total)](https://img.shields.io/github/commit-activity/t/vhogemann/social-worker?logo=github)](https://github.com/vhogemann/social-worker/graphs/commit-activity)
[![GitHub Commit Activity (Monthly)](https://img.shields.io/github/commit-activity/m/vhogemann/social-worker?logo=github)](https://github.com/vhogemann/social-worker/graphs/commit-activity)
[![GitHub Repo Size](https://img.shields.io/github/repo-size/vhogemann/social-worker?logo=github)](https://github.com/vhogemann/social-worker)
[![GitHub Code Size](https://img.shields.io/github/languages/code-size/vhogemann/social-worker?logo=github)](https://github.com/vhogemann/social-worker)
[![GitHub Language Count](https://img.shields.io/github/languages/count/vhogemann/social-worker?logo=github)](https://github.com/vhogemann/social-worker)
[![GitHub Top Language](https://img.shields.io/github/languages/top/vhogemann/social-worker?logo=github)](https://github.com/vhogemann/social-worker)
[![GitHub Contributors](https://img.shields.io/github/contributors/vhogemann/social-worker?logo=github)](https://github.com/vhogemann/social-worker/graphs/contributors)
[![GitHub Repo Created](https://img.shields.io/github/created-at/vhogemann/social-worker?logo=github)](https://github.com/vhogemann/social-worker)
[![GitHub Release](https://img.shields.io/github/v/release/vhogemann/social-worker?logo=github)](https://github.com/vhogemann/social-worker/releases)
[![GitHub Release Date](https://img.shields.io/github/release-date/vhogemann/social-worker?logo=github)](https://github.com/vhogemann/social-worker/releases)
[![GitHub Downloads (All Releases)](https://img.shields.io/github/downloads/vhogemann/social-worker/total?logo=github)](https://github.com/vhogemann/social-worker/releases)
[![GitHub Downloads (Latest)](https://img.shields.io/github/downloads/vhogemann/social-worker/latest/total?logo=github)](https://github.com/vhogemann/social-worker/releases/latest)
[![GitHub Milestones](https://img.shields.io/github/milestones/all/vhogemann/social-worker?logo=github)](https://github.com/vhogemann/social-worker/milestones)
[![GitHub Open Milestones](https://img.shields.io/github/milestones/open/vhogemann/social-worker?logo=github)](https://github.com/vhogemann/social-worker/milestones?state=open)
[![GitHub Closed Milestones](https://img.shields.io/github/milestones/closed/vhogemann/social-worker?logo=github)](https://github.com/vhogemann/social-worker/milestones?state=closed)

### Maintainer

[![GitHub Followers](https://img.shields.io/github/followers/vhogemann?logo=github)](https://github.com/vhogemann?tab=followers)
[![GitHub User Stars](https://img.shields.io/github/stars/vhogemann?style=social)](https://github.com/vhogemann?tab=stars)
[![GitHub Sponsors](https://img.shields.io/github/sponsors/vhogemann?logo=github)](https://github.com/sponsors/vhogemann)

---

## Technical Stack

- **Frontend**: Vite + React + TypeScript + Tailwind. State management via Zustand, server state synchronization via TanStack React Query, and SSE streaming chat via native `EventSource`.
- **Backend**: .NET 10 ASP.NET Core Minimal APIs + Entity Framework Core + Postgres 16.
- **AI Integrations**: Built using `Microsoft.Extensions.AI` and the `OpenAI` .NET SDK, supporting OpenAI, OpenRouter, and Ollama.

---

## Features

- **Multi-Modal Assistant Chat**: Interactive composer panel driven by LLM agents. Features dynamic tool registrations based on model capabilities (vision/tool calls).
- **Auto-Summarization**: Automatically generates title summaries for newly created drafts based on initial composer prompts.
- **Real-Time Thread Preview**: Instantly preview how segments appear as social media cards. Supports clipboard segment copying and inline alt-text configuration.
- **Format Validation**: Active validation loops prevent incompatible post stages (e.g., warning if a post segment combines both image assets and YouTube embeds, violating Bluesky formatting constraints).
- **Resource Deduplication (SHA-256)**: Files and images are fingerprinted on upload. If duplicate files are detected, database entities reuse the existing payload to prevent disk bloat.
- **Clean Deletion Protection**: Safety confirmations before deletions, with clean active draft switching or new fallbacks. Shared uploads are protected on disk during asset/draft deletions.
- **Admin Settings**: Manage LLM providers, test connections, set user account details, and toggle preferences.

---

## Getting Started

### Prerequisites

Ensure you have the following installed on your host machine:
1. **Docker** (with Compose v2+)

### 1. Launch Services

Start the development stack:

```bash
docker compose up --build
```

If you have an NVIDIA GPU and want Whisper hardware acceleration in the transcriber:

```bash
docker compose -f docker-compose.yml -f docker-compose.gpu.yml up --build
```

- **Web App**: `http://localhost:8100`
- **API**: `http://localhost:8101`
- **Transcriber (Whisper)**: `http://localhost:8102`

The transcriber service runs a local Whisper model inside Docker and stores transcript artifacts in `./transcripts/`.

### 1.1 Run E2E Stack Separately (No Port Collisions)

E2E now has a dedicated stack file that runs on separate host ports:

```bash
docker compose -f docker-compose.e2e.yml up -d --build
```

To run e2e with transcriber GPU acceleration:

```bash
docker compose -f docker-compose.e2e.yml -f docker-compose.e2e.gpu.yml up -d --build
```

- **E2E Web App**: `http://localhost:8200`
- **E2E API**: `http://localhost:8201`

One-command e2e runner:

```bash
./scripts/run-e2e.sh
```

Examples:

```bash
# Single test file
./scripts/run-e2e.sh tests/chat.spec.ts

# Keep stack up for debugging after run
./scripts/run-e2e.sh --keep-up tests/chat.spec.ts

# Build images first
./scripts/run-e2e.sh --build
```

Safer update flow for running containers:

```bash
./scripts/redeploy.sh
```

This performs `docker compose down --remove-orphans` followed by `docker compose up -d --build`.

For a UI walkthrough with screenshots, see [GETTING_STARTED.md](GETTING_STARTED.md).

### 2. Run Only (Prebuilt Images, No Local Build)

If you just want to run the app and not develop it locally, use the prebuilt images:

```bash
docker compose -f docker-compose.app.yml up -d
```

Or use the helper launcher with env preflight checks:

```bash
./scripts/run-app.sh
```

To force-refresh to the latest published images:

```bash
docker compose -f docker-compose.app.yml pull
docker compose -f docker-compose.app.yml up -d
```

Helper equivalent:

```bash
./scripts/run-app.sh --pull
```

Safer update flow for the app-image stack:

```bash
./scripts/redeploy.sh --app --pull
```

- **Web App**: `http://localhost:8100`
- **API**: `http://localhost:8101`

---

## Workspace Layout

```
social-worker/
├── docker-compose.yml              # Base Docker Compose stack definition
├── docker-compose.app.yml          # Runtime-only stack using published GHCR images
├── docker-compose.gpu.yml          # GPU resource reservation overrides
├── transcriber/                    # Python FastAPI transcript service (yt-dlp + Whisper)
├── transcripts/                    # Local transcript artifacts (gitignored except .gitkeep)
├── scripts/
│   └── run-app.sh                  # One-command runtime launcher with preflight checks
│   └── redeploy.sh                 # Safer down/up redeploy helper for dev and app stacks
├── .env.example                    # Template environment file
├── AGENTS.md                       # Agent guidelines and specifications
├── PLAN.md                         # Detailed project roadmap and index
├── planning/                      # Implementation plans and design assets
├── api/                            # Backend C# .NET Minimal API
└── web/                            # Frontend Vite React Single Page App
```

---

## Development Guidelines

All builds, migration generations, and unit tests must be executed inside Docker to ensure host runtime parity:

- **API Build**: `docker compose exec api dotnet build`
- **Frontend Build**: `docker compose exec web sh -c "npm run build && npm run typecheck"`
- **Unit Tests**: `docker compose exec api dotnet test`
- **EF Core Migrations**: `docker compose exec api dotnet ef migrations add <MigrationName>`

## Documentation

- **Getting Started Guide**: [GETTING_STARTED.md](GETTING_STARTED.md)
- **Regenerate Guide + Screenshots**: `./scripts/regenerate-getting-started.sh`

---

