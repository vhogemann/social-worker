# social-worker 🛠️

A local-first, Docker-only multi-modal assistant for composing and publishing social media **threads**. 

Designed to support creators with LLM assistance, `social-worker` lets you draft threads, attach sources and media, adapt variants to multiple platforms, and publish. Version 1 ships with end-to-end publishing support for Bluesky, with stubs ready for Twitter, LinkedIn, Facebook, and Instagram.

---

## Technical Stack

- **Frontend**: Vite + React + TypeScript + Tailwind. State management via Zustand, server state synchronization via TanStack React Query, and SSE streaming chat via native `EventSource`.
- **Backend**: .NET 10 ASP.NET Core Minimal APIs + Entity Framework Core + Postgres 16.
- **AI Integrations**: Built using `Microsoft.Extensions.AI` and the `OpenAI` .NET SDK, supporting OpenAI, OpenRouter, and Ollama.
- **Local Proxy & HTTPS**: Caddy + `mkcert` for host-safe domain resolution and SSL termination.

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
2. **mkcert** (local HTTPS certificate utility)

### 1. Certificate Setup

Run the bootstrap script once on the host machine to generate self-signed certs for local domains:

```bash
./scripts/bootstrap.sh
```

This creates SSL certificates mapped to:
- `https://social-worker.localtest` — Web Client Application
- `https://api.social-worker.localtest` — Backend REST API
- `https://db.social-worker.localtest` — Database Manager (Adminer)

### 2. Launch Services

Start the development stack under Docker:

```bash
# CPU mode (Mac or Linux without GPU)
docker compose --profile local-llm up --build

# Linux + NVidia GPU mode (requires nvidia-container-toolkit on host)
docker compose --profile local-llm -f docker-compose.yml -f docker-compose.gpu.yml up --build
```

### 3. Initialize Local LLM Model (Optional)

If running a local Ollama model:

```bash
docker exec social-worker-ollama ollama pull llama3.1
```

---

## Workspace Layout

```
social-worker/
├── docker-compose.yml              # Base Docker Compose stack definition
├── docker-compose.gpu.yml          # GPU resource reservation overrides
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
