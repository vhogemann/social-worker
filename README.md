# social-worker

A local-first, Docker-only assistant for drafting, refining, and publishing social media threads.

`social-worker` combines an AI-powered conversational composer with multi-platform publishing, automated feed ingestion (RSS, YouTube, Instagram), media transcription (Whisper), content scheduling, and autonomous reply agents.

Supported publishing platforms:
- **Bluesky**: Multi-post threads, rich embeds, facets.
- **LinkedIn**: Member profiles and company pages.
- **Meta Threads**: Multi-post threads, carousel media, token lifecycle management.
- **Twitter / X**: OAuth 2.0 PKCE, chained reply threads, weighted character validation.

---

## Architecture & Tech Stack

- **Frontend** (`web/`): React 19, TypeScript, Vite, Tailwind CSS, Zustand, TanStack React Query.
- **Backend API** (`api/`): .NET 10 ASP.NET Core Minimal APIs, Entity Framework Core, PostgreSQL 16.
- **AI Integrations**: Built with `Microsoft.Extensions.AI` and OpenAI-compatible SDKs (OpenRouter, OpenAI, Ollama).
- **Transcriber** (`transcriber/`): Python 3.11, FastAPI, Whisper, `yt-dlp` for extracting and transcribing audio/video.
- **Image Engine** (`image-engine/`): Node.js 22, Fastify, Sharp, `@napi-rs/canvas` for template rendering.
- **Search & Discovery**: Integrated SearXNG search engine instance.
- **Protocol & Integrations**: Full Model Context Protocol (MCP) server support (JSON-RPC 2.0 & SSE).

---

## Local Configuration & Setup

### Prerequisites

- **Docker** with **Docker Compose v2+** installed.
- (Optional) NVIDIA GPU drivers & NVIDIA Container Toolkit if running local Whisper or Ollama with GPU acceleration.

### 1. Configure Environment Variables

Copy the template `.env.example` file to `.env`:

```bash
cp .env.example .env
```

Open `.env` and configure the following parameters:

#### Core Requirements

1. **LLM Provider**
   Configure your preferred OpenAI-compatible model provider:
   ```env
   LLM__BaseUrl=https://openrouter.ai/api/v1
   LLM__ApiKey=your_api_key_here
   LLM__Model=anthropic/claude-3.5-sonnet
   ```
   *For local Ollama*: set `LLM__BaseUrl=http://ollama:11434/v1`, `LLM__ApiKey=dummy`, `LLM__Model=llama3.1`.

2. **Security & Authentication**
   ```env
   Auth__JwtSecret=replace_with_a_secret_at_least_32_characters_long
   Auth__AdminPassword=your_admin_password
   Auth__DbEncryptionKey=generated_base64_encryption_key
   ```
   Generate a 256-bit AES key for `Auth__DbEncryptionKey` (used for encrypting platform tokens at rest):
   ```bash
   openssl rand -base64 32
   ```

#### Optional Integrations

- **Search**: Set `Search__Provider=SearXng` (default, uses bundled container) or `Search__Provider=Brave` with `Search__BraveApiKey`.
- **Social Platform OAuth**: Add client IDs and secrets for LinkedIn, Meta Threads, Twitter/X, and Instagram if you plan to connect these accounts locally. See [docs/INSTAGRAM_SETUP.md](docs/INSTAGRAM_SETUP.md) for Instagram Business Discovery setup.

---

## Running the Application

### Option A: Standard Production / Local Stack

Build and start all services:

```bash
docker compose up --build
```

With GPU acceleration for Whisper transcription:

```bash
docker compose -f docker-compose.yml -f docker-compose.gpu.yml up --build
```

### Option B: Fast Hot-Reloading Development Stack

For active development with Vite HMR, `dotnet watch`, and live-reloading services:

```bash
./scripts/run-dev.sh
```

### Option C: Run Prebuilt Images

To launch without building locally from source:

```bash
./scripts/run-app.sh
```

### Accessing Endpoints

Once running, the stack provides:

| Service | URL | Notes |
| :--- | :--- | :--- |
| **Web App** | [http://localhost:8100](http://localhost:8100) | UI composer, drafts, calendar, outbox, settings |
| **Backend API** | [http://localhost:8101](http://localhost:8101) | REST endpoints, Swagger/OpenAPI |
| **Whisper Transcriber** | [http://localhost:8102](http://localhost:8102) | FastAPI audio/video transcription service |
| **Image Engine** | [http://localhost:8103](http://localhost:8103) | Fastify template rendering service |
| **MCP Server** | `http://localhost:8101/api/mcp` | Model Context Protocol endpoint (Stateless & SSE) |

To safely stop and restart containers after changes:

```bash
./scripts/redeploy.sh
```

---

## Testing & Quality Assurance

All builds and tests run inside Docker containers:

- **API Build**:
  ```bash
  docker compose --profile tooling run --rm dotnet build
  ```
- **API Tests**:
  ```bash
  docker compose --profile tooling run --rm dotnet test SocialWorker.Api.Tests/SocialWorker.Api.Tests.csproj
  ```
- **Web Tests**:
  ```bash
  docker compose --profile tooling run --rm web-test
  ```
- **Web Typecheck & Build**:
  ```bash
  docker compose --profile tooling run --rm web-test "npm run build && npm run typecheck"
  ```
- **Image Engine Tests**:
  ```bash
  docker compose --profile tooling run --rm image-engine-test
  ```
- **End-to-End Tests**:
  ```bash
  ./scripts/run-e2e.sh
  ```

---

## Database Utilities

Query the database directly inside Docker using the SQL helper script:

```bash
./scripts/sql.sh -c "SELECT now();"
./scripts/sql.sh -c "SELECT \"Id\", \"Title\", \"Stage\", \"Status\" FROM \"Drafts\" LIMIT 5;"
```

To add EF Core migrations:

```bash
docker compose --profile tooling run --rm dotnet ef migrations add <MigrationName>
```

---

## Documentation & Reference

- [MENTAL_MODEL.md](MENTAL_MODEL.md): Technical architecture map, file ownership, and conventions.
- [AGENTS.md](AGENTS.md): Developer policies, workflow constraints, and coding standards.
- [PLAN.md](PLAN.md): Active roadmap, feature tracking, and technical design documents.
- [docs/mcp_server.md](docs/mcp_server.md): Model Context Protocol (MCP) server reference and tool catalogue.
- [docs/INSTAGRAM_SETUP.md](docs/INSTAGRAM_SETUP.md): Guide for Meta Business Discovery and Instagram source configuration.
